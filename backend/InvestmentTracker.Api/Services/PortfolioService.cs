// Services/PortfolioService.cs
// All the math and "business logic" lives here.
//
// IMPORTANT design rule: Controllers don't do calculations. Services do.
// That separation lets us unit-test the math without standing up a web server.
//
// We use the WEIGHTED-AVERAGE COST method to compute cost basis - the simplest approach,
// and what most casual investing apps show by default.

using InvestmentTracker.Api.Data;
using InvestmentTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Api.Services;

public interface IPortfolioService
{
    Task<List<HoldingDto>> GetHoldingsAsync();
    Task<PortfolioSummaryDto> GetSummaryAsync();
    Task<Transaction> AddTransactionAsync(CreateTransactionDto dto);
    Task<List<Transaction>> GetTransactionsAsync();
    Task DeleteTransactionAsync(int id);
}

public class PortfolioService : IPortfolioService
{
    private readonly AppDbContext _db;
    private readonly IMarketDataService _market;

    public PortfolioService(AppDbContext db, IMarketDataService market)
    {
        _db = db;
        _market = market;
    }

    public async Task<Transaction> AddTransactionAsync(CreateTransactionDto dto)
    {
        // Normalize the symbol so "btc", "BTC", and "Btc" all collapse to one holding.
        var symbol = dto.AssetType == AssetType.Crypto
            ? dto.Symbol.Trim().ToLowerInvariant()
            : dto.Symbol.Trim().ToUpperInvariant();

        var tx = new Transaction
        {
            Symbol = symbol,
            AssetType = dto.AssetType,
            Type = dto.Type,
            Quantity = dto.Quantity,
            PricePerUnit = dto.PricePerUnit,
            Date = DateTime.UtcNow
        };

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();
        return tx;
    }

    public async Task<List<Transaction>> GetTransactionsAsync() =>
        await _db.Transactions.OrderByDescending(t => t.Date).ToListAsync();

    public async Task DeleteTransactionAsync(int id)
    {
        var tx = await _db.Transactions.FindAsync(id);
        if (tx != null)
        {
            _db.Transactions.Remove(tx);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<HoldingDto>> GetHoldingsAsync()
    {
        var transactions = await _db.Transactions.ToListAsync();

        // Group by (symbol, assetType) so we collapse multiple buys/sells into one row.
        var grouped = transactions.GroupBy(t => new { t.Symbol, t.AssetType });

        // Fetch all current prices in one batch.
        var prices = await _market.GetPricesAsync(
            grouped.Select(g => (g.Key.Symbol, g.Key.AssetType)));

        var holdings = new List<HoldingDto>();
        foreach (var group in grouped)
        {
            var (qty, avgCost) = ComputeQuantityAndAverageCost(group);
            if (qty <= 0) continue; // user has fully sold this asset; skip

            var priceKey = $"{group.Key.AssetType}:{group.Key.Symbol}";
            var currentPrice = prices.GetValueOrDefault(priceKey, 0m);

            var totalCost = qty * avgCost;
            var marketValue = qty * currentPrice;
            var gain = marketValue - totalCost;
            var gainPct = totalCost == 0 ? 0 : gain / totalCost * 100m;

            holdings.Add(new HoldingDto(
                Symbol: group.Key.Symbol,
                AssetType: group.Key.AssetType,
                Quantity: qty,
                AverageCost: avgCost,
                CurrentPrice: currentPrice,
                MarketValue: marketValue,
                TotalCost: totalCost,
                UnrealizedGain: gain,
                UnrealizedGainPercent: gainPct
            ));
        }

        return holdings.OrderByDescending(h => h.MarketValue).ToList();
    }

    public async Task<PortfolioSummaryDto> GetSummaryAsync()
    {
        var holdings = await GetHoldingsAsync();
        var transactions = await _db.Transactions.ToListAsync();

        // Realized gain = the running total of profit on sells, computed at the time of each sell
        // using the running average cost basis. This walks transactions in date order per asset.
        var realized = ComputeRealizedGain(transactions);

        var totalCost = holdings.Sum(h => h.TotalCost);
        var marketValue = holdings.Sum(h => h.MarketValue);
        var unrealized = marketValue - totalCost;
        var unrealizedPct = totalCost == 0 ? 0 : unrealized / totalCost * 100m;

        return new PortfolioSummaryDto(
            TotalCost: totalCost,
            MarketValue: marketValue,
            UnrealizedGain: unrealized,
            UnrealizedGainPercent: unrealizedPct,
            RealizedGain: realized,
            HoldingCount: holdings.Count
        );
    }

    // -------- Internal calculation helpers --------

    // Walks transactions in date order keeping a running quantity and running average cost.
    // On a buy:  newAvg = (oldQty*oldAvg + buyQty*buyPrice) / (oldQty + buyQty)
    // On a sell: quantity decreases, average cost is unchanged.
    private static (decimal Quantity, decimal AverageCost) ComputeQuantityAndAverageCost(IEnumerable<Transaction> txs)
    {
        decimal qty = 0m;
        decimal avg = 0m;

        foreach (var tx in txs.OrderBy(t => t.Date))
        {
            if (tx.Type == TransactionType.Buy)
            {
                var newQty = qty + tx.Quantity;
                avg = newQty == 0 ? 0 : (qty * avg + tx.Quantity * tx.PricePerUnit) / newQty;
                qty = newQty;
            }
            else // Sell
            {
                qty -= tx.Quantity;
                if (qty <= 0) { qty = 0; avg = 0; }
            }
        }

        return (qty, avg);
    }

    private static decimal ComputeRealizedGain(IEnumerable<Transaction> txs)
    {
        decimal realized = 0m;

        // Process each (Symbol, AssetType) independently.
        foreach (var group in txs.GroupBy(t => new { t.Symbol, t.AssetType }))
        {
            decimal qty = 0m;
            decimal avg = 0m;

            foreach (var tx in group.OrderBy(t => t.Date))
            {
                if (tx.Type == TransactionType.Buy)
                {
                    var newQty = qty + tx.Quantity;
                    avg = newQty == 0 ? 0 : (qty * avg + tx.Quantity * tx.PricePerUnit) / newQty;
                    qty = newQty;
                }
                else
                {
                    // Profit on this sell = (sellPrice - avgCost) * qtySold.
                    realized += (tx.PricePerUnit - avg) * tx.Quantity;
                    qty -= tx.Quantity;
                    if (qty <= 0) { qty = 0; avg = 0; }
                }
            }
        }

        return realized;
    }
}
