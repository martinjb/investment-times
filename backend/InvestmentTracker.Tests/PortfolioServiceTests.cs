// PortfolioServiceTests.cs
// Unit tests for the heart of the app: the cost-basis and P/L math.
//
// Patterns demonstrated:
//   * AAA (Arrange, Act, Assert) - the standard unit-test structure.
//   * Mocking external dependencies with Moq - we stub IMarketDataService so tests don't need the internet.
//   * In-memory EF Core DbContext - real EF behaviour, fake storage. Fast and deterministic.
//   * FluentAssertions for readable assertions: result.Should().Be(...).
//   * "[Theory]" + "[InlineData]" for table-driven tests.

using FluentAssertions;
using InvestmentTracker.Api.Models;
using InvestmentTracker.Api.Services;
using Moq;
using Xunit;

namespace InvestmentTracker.Tests;

public class PortfolioServiceTests
{
    // Helper: build a service with the DB pre-seeded and the market service stubbed
    // to return whatever fixed prices the test cares about.
    private static (PortfolioService Service, Mock<IMarketDataService> MarketMock) CreateSut(
        Dictionary<string, decimal>? fixedPrices = null,
        params Transaction[] seedTransactions)
    {
        var db = TestHelpers.CreateInMemoryDb();
        if (seedTransactions.Length > 0)
        {
            db.Transactions.AddRange(seedTransactions);
            db.SaveChanges();
        }

        var marketMock = new Mock<IMarketDataService>();

        // GetPricesAsync: return the fixed price dictionary if provided, otherwise empty.
        marketMock
            .Setup(m => m.GetPricesAsync(It.IsAny<IEnumerable<(string Symbol, AssetType Type)>>()))
            .ReturnsAsync(fixedPrices ?? new Dictionary<string, decimal>());

        // GetPriceAsync: look up by key.
        marketMock
            .Setup(m => m.GetPriceAsync(It.IsAny<string>(), It.IsAny<AssetType>()))
            .ReturnsAsync((string sym, AssetType t) =>
                fixedPrices != null && fixedPrices.TryGetValue($"{t}:{sym}", out var p) ? p : 0m);

        return (new PortfolioService(db, marketMock.Object), marketMock);
    }

    // -------------------------------------------------------------------------
    //   AddTransactionAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddTransaction_PersistsTransaction_AndNormalizesStockSymbolToUpper()
    {
        // Arrange
        var (sut, _) = CreateSut();
        var dto = new CreateTransactionDto("aapl", AssetType.Stock, TransactionType.Buy, 10m, 150m);

        // Act
        var saved = await sut.AddTransactionAsync(dto);

        // Assert
        saved.Symbol.Should().Be("AAPL");
        saved.Quantity.Should().Be(10m);
        saved.Type.Should().Be(TransactionType.Buy);
        saved.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddTransaction_NormalizesCryptoSymbolToLower()
    {
        var (sut, _) = CreateSut();
        var dto = new CreateTransactionDto("Bitcoin", AssetType.Crypto, TransactionType.Buy, 1m, 50000m);

        var saved = await sut.AddTransactionAsync(dto);

        saved.Symbol.Should().Be("bitcoin");
    }

    // -------------------------------------------------------------------------
    //   GetHoldingsAsync - core math
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetHoldings_SingleBuy_ReturnsOneHoldingWithCorrectMath()
    {
        // 10 AAPL bought at $100, current price $150
        // Expected: cost = $1000, value = $1500, gain = $500 (+50%)
        var prices = new Dictionary<string, decimal> { ["Stock:AAPL"] = 150m };
        var (sut, _) = CreateSut(prices,
            BuildTx("AAPL", AssetType.Stock, TransactionType.Buy, 10m, 100m));

        var holdings = await sut.GetHoldingsAsync();

        holdings.Should().HaveCount(1);
        var h = holdings[0];
        h.Symbol.Should().Be("AAPL");
        h.Quantity.Should().Be(10m);
        h.AverageCost.Should().Be(100m);
        h.CurrentPrice.Should().Be(150m);
        h.TotalCost.Should().Be(1000m);
        h.MarketValue.Should().Be(1500m);
        h.UnrealizedGain.Should().Be(500m);
        h.UnrealizedGainPercent.Should().Be(50m);
    }

    [Fact]
    public async Task GetHoldings_MultipleBuys_UsesWeightedAverageCost()
    {
        // 10 shares @ $100 then 10 shares @ $200 -> avg cost should be $150.
        // 20 shares @ current $180 -> value $3600, cost $3000, gain $600 (+20%)
        var prices = new Dictionary<string, decimal> { ["Stock:MSFT"] = 180m };
        var (sut, _) = CreateSut(prices,
            BuildTx("MSFT", AssetType.Stock, TransactionType.Buy, 10m, 100m, daysAgo: 30),
            BuildTx("MSFT", AssetType.Stock, TransactionType.Buy, 10m, 200m, daysAgo: 15));

        var holdings = await sut.GetHoldingsAsync();

        var h = holdings.Single();
        h.Quantity.Should().Be(20m);
        h.AverageCost.Should().Be(150m);
        h.MarketValue.Should().Be(3600m);
        h.UnrealizedGain.Should().Be(600m);
        h.UnrealizedGainPercent.Should().Be(20m);
    }

    [Fact]
    public async Task GetHoldings_PartialSell_ReducesQuantityWithoutChangingAvgCost()
    {
        // Buy 10 @ $100. Sell 4 @ $200. Should still hold 6 @ avg cost $100.
        var prices = new Dictionary<string, decimal> { ["Stock:GOOG"] = 250m };
        var (sut, _) = CreateSut(prices,
            BuildTx("GOOG", AssetType.Stock, TransactionType.Buy, 10m, 100m, daysAgo: 30),
            BuildTx("GOOG", AssetType.Stock, TransactionType.Sell, 4m, 200m, daysAgo: 5));

        var holdings = await sut.GetHoldingsAsync();

        var h = holdings.Single();
        h.Quantity.Should().Be(6m);
        h.AverageCost.Should().Be(100m);     // unchanged by sells
        h.MarketValue.Should().Be(1500m);    // 6 * 250
    }

    [Fact]
    public async Task GetHoldings_FullSell_ExcludesAssetFromHoldings()
    {
        // Buy 5, sell all 5. The position is closed - shouldn't appear.
        var (sut, _) = CreateSut(
            fixedPrices: new Dictionary<string, decimal> { ["Crypto:bitcoin"] = 60000m },
            BuildTx("bitcoin", AssetType.Crypto, TransactionType.Buy, 5m, 50000m, daysAgo: 30),
            BuildTx("bitcoin", AssetType.Crypto, TransactionType.Sell, 5m, 70000m, daysAgo: 1));

        var holdings = await sut.GetHoldingsAsync();

        holdings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHoldings_GroupsByAssetTypeAndSymbol()
    {
        // Stock AAPL and crypto with id "aapl-token" should be two separate holdings,
        // even though the symbols look similar - assetType disambiguates them.
        var prices = new Dictionary<string, decimal>
        {
            ["Stock:AAPL"]        = 150m,
            ["Crypto:aapl-token"] = 2m
        };
        var (sut, _) = CreateSut(prices,
            BuildTx("AAPL",       AssetType.Stock,  TransactionType.Buy, 1m, 100m),
            BuildTx("aapl-token", AssetType.Crypto, TransactionType.Buy, 1m, 1m));

        var holdings = await sut.GetHoldingsAsync();

        holdings.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHoldings_OrdersByMarketValueDescending()
    {
        // Three holdings of clearly different values. Largest should come first.
        var prices = new Dictionary<string, decimal>
        {
            ["Stock:SMALL"] = 10m,
            ["Stock:BIG"]   = 1000m,
            ["Stock:MID"]   = 100m
        };
        var (sut, _) = CreateSut(prices,
            BuildTx("SMALL", AssetType.Stock, TransactionType.Buy, 1m, 5m),
            BuildTx("BIG",   AssetType.Stock, TransactionType.Buy, 1m, 500m),
            BuildTx("MID",   AssetType.Stock, TransactionType.Buy, 1m, 50m));

        var holdings = await sut.GetHoldingsAsync();

        holdings.Select(h => h.Symbol).Should().ContainInOrder("BIG", "MID", "SMALL");
    }

    // -------------------------------------------------------------------------
    //   GetSummaryAsync - aggregate stats
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSummary_SumsCostAndValueAcrossAllHoldings()
    {
        var prices = new Dictionary<string, decimal>
        {
            ["Stock:AAPL"] = 150m,
            ["Stock:MSFT"] = 200m
        };
        var (sut, _) = CreateSut(prices,
            BuildTx("AAPL", AssetType.Stock, TransactionType.Buy, 10m, 100m),
            BuildTx("MSFT", AssetType.Stock, TransactionType.Buy, 5m,  150m));

        var summary = await sut.GetSummaryAsync();

        summary.HoldingCount.Should().Be(2);
        summary.TotalCost.Should().Be(1750m);   // 1000 + 750
        summary.MarketValue.Should().Be(2500m); // 1500 + 1000
        summary.UnrealizedGain.Should().Be(750m);
    }

    [Fact]
    public async Task GetSummary_RealizedGain_IsProfitFromCompletedSells()
    {
        // Buy 10 @ $100 ($1000 cost). Sell 4 @ $200 ($800 proceeds).
        // Realized profit = (200 - 100) * 4 = $400
        var prices = new Dictionary<string, decimal> { ["Stock:NVDA"] = 110m };
        var (sut, _) = CreateSut(prices,
            BuildTx("NVDA", AssetType.Stock, TransactionType.Buy,  10m, 100m, daysAgo: 30),
            BuildTx("NVDA", AssetType.Stock, TransactionType.Sell,  4m, 200m, daysAgo: 5));

        var summary = await sut.GetSummaryAsync();

        summary.RealizedGain.Should().Be(400m);
    }

    [Fact]
    public async Task GetSummary_RealizedGain_CanBeNegative()
    {
        // Bought at $200, sold at $100 - a $100 loss per unit, 5 units = -$500.
        var prices = new Dictionary<string, decimal> { ["Stock:LOSS"] = 50m };
        var (sut, _) = CreateSut(prices,
            BuildTx("LOSS", AssetType.Stock, TransactionType.Buy,  10m, 200m, daysAgo: 30),
            BuildTx("LOSS", AssetType.Stock, TransactionType.Sell,  5m, 100m, daysAgo: 5));

        var summary = await sut.GetSummaryAsync();

        summary.RealizedGain.Should().Be(-500m);
    }

    [Fact]
    public async Task GetSummary_EmptyPortfolio_ReturnsAllZeros()
    {
        var (sut, _) = CreateSut();

        var summary = await sut.GetSummaryAsync();

        summary.HoldingCount.Should().Be(0);
        summary.TotalCost.Should().Be(0);
        summary.MarketValue.Should().Be(0);
        summary.UnrealizedGain.Should().Be(0);
        summary.UnrealizedGainPercent.Should().Be(0);
        summary.RealizedGain.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    //   Edge cases
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(100, 100, 0)]    // bought and unchanged: 0% gain
    [InlineData(100, 200, 100)]  // doubled: +100%
    [InlineData(100, 50, -50)]   // halved: -50%
    [InlineData(100, 110, 10)]   // +10%
    public async Task GetHoldings_GainPercent_CalculatedCorrectly(
        decimal buyPrice, decimal currentPrice, decimal expectedPct)
    {
        var prices = new Dictionary<string, decimal> { ["Stock:TEST"] = currentPrice };
        var (sut, _) = CreateSut(prices,
            BuildTx("TEST", AssetType.Stock, TransactionType.Buy, 1m, buyPrice));

        var holdings = await sut.GetHoldingsAsync();

        holdings.Single().UnrealizedGainPercent.Should().Be(expectedPct);
    }

    [Fact]
    public async Task GetHoldings_WhenMarketServiceReturnsZeroPrice_GainIsNegativeFullCost()
    {
        // If price lookup fails (returns 0), market value is 0 - we lose 100% on paper.
        var (sut, _) = CreateSut(
            fixedPrices: new Dictionary<string, decimal>(), // no prices for anything
            BuildTx("UNKNOWN", AssetType.Stock, TransactionType.Buy, 10m, 50m));

        var holdings = await sut.GetHoldingsAsync();

        var h = holdings.Single();
        h.CurrentPrice.Should().Be(0);
        h.UnrealizedGain.Should().Be(-500m);
        h.UnrealizedGainPercent.Should().Be(-100m);
    }

    [Fact]
    public async Task DeleteTransaction_RemovesItFromLedger()
    {
        var (sut, _) = CreateSut();
        var saved = await sut.AddTransactionAsync(
            new CreateTransactionDto("AAPL", AssetType.Stock, TransactionType.Buy, 1m, 100m));

        await sut.DeleteTransactionAsync(saved.Id);

        var all = await sut.GetTransactionsAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTransaction_NonExistentId_DoesNotThrow()
    {
        var (sut, _) = CreateSut();
        var act = async () => await sut.DeleteTransactionAsync(999_999);
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    //   Internal helper
    // -------------------------------------------------------------------------

    private static Transaction BuildTx(
        string symbol, AssetType assetType, TransactionType type,
        decimal quantity, decimal price, int daysAgo = 1)
    {
        return new Transaction
        {
            Symbol = symbol,
            AssetType = assetType,
            Type = type,
            Quantity = quantity,
            PricePerUnit = price,
            Date = DateTime.UtcNow.AddDays(-daysAgo)
      