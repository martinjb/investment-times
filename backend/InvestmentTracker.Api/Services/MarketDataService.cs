// Services/MarketDataService.cs
// Talks to free public APIs to get live prices.
//
//   * CoinGecko for crypto - no API key required.
//   * Yahoo Finance (query1.finance.yahoo.com) for stocks and indices - no key required.
//
// Design pattern: this is the "Service" layer (sometimes called "Application Service" or "Use Case" layer).
// Controllers should be thin and delegate real work to services like this one.
//
// We define an INTERFACE (IMarketDataService) and a CLASS that implements it. The controller depends
// on the interface, not the class. That makes it trivial to swap in a fake implementation in unit tests.

using System.Text.Json;
using InvestmentTracker.Api.Models;

namespace InvestmentTracker.Api.Services;

public interface IMarketDataService
{
    Task<List<MarketIndicatorDto>> GetLandingIndicatorsAsync();
    Task<decimal> GetPriceAsync(string symbol, AssetType assetType);
    Task<Dictionary<string, decimal>> GetPricesAsync(IEnumerable<(string Symbol, AssetType Type)> assets);
}

public class MarketDataService : IMarketDataService
{
    private readonly HttpClient _http;
    private readonly ILogger<MarketDataService> _logger;

    // Tiny in-memory cache so we don't hammer the public APIs on every request.
    // For a real app, use IMemoryCache or a distributed cache (Redis).
    private static readonly Dictionary<string, (decimal Price, DateTime Fetched)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public MarketDataService(IHttpClientFactory httpFactory, ILogger<MarketDataService> logger)
    {
        _http = httpFactory.CreateClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "InvestmentTracker/1.0");
        _logger = logger;
    }

    // The four indicators on the landing page: BTC, S&P 500, Dow Jones, Brent Crude.
    public async Task<List<MarketIndicatorDto>> GetLandingIndicatorsAsync()
    {
        var results = new List<MarketIndicatorDto>();

        // BTC via CoinGecko.
        try
        {
            var btc = await GetCryptoQuoteAsync("bitcoin");
            results.Add(new MarketIndicatorDto("Bitcoin", "BTC", btc.Price, btc.ChangePct));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "BTC fetch failed"); }

        // Stocks/indices via Yahoo. ^GSPC = S&P 500, ^DJI = Dow Jones, BZ=F = Brent crude futures.
        var yahooTickers = new[]
        {
            ("S&P 500",     "^GSPC"),
            ("Dow Jones",   "^DJI"),
            ("Brent Crude", "BZ=F")
        };

        foreach (var (name, ticker) in yahooTickers)
        {
            try
            {
                var quote = await GetYahooQuoteAsync(ticker);
                results.Add(new MarketIndicatorDto(name, ticker, quote.Price, quote.ChangePct));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Yahoo fetch failed for {Ticker}", ticker); }
        }

        return results;
    }

    public async Task<decimal> GetPriceAsync(string symbol, AssetType assetType)
    {
        var cacheKey = $"{assetType}:{symbol}";
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.Fetched < CacheTtl)
            return cached.Price;

        decimal price;
        if (assetType == AssetType.Crypto)
            price = (await GetCryptoQuoteAsync(symbol.ToLower())).Price;
        else
            price = (await GetYahooQuoteAsync(symbol.ToUpper())).Price;

        _cache[cacheKey] = (price, DateTime.UtcNow);
        return price;
    }

    public async Task<Dictionary<string, decimal>> GetPricesAsync(IEnumerable<(string Symbol, AssetType Type)> assets)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var asset in assets.Distinct())
        {
            try
            {
                result[$"{asset.Type}:{asset.Symbol}"] = await GetPriceAsync(asset.Symbol, asset.Type);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Price fetch failed for {Symbol}", asset.Symbol);
                result[$"{asset.Type}:{asset.Symbol}"] = 0m;
            }
        }
        return result;
    }

    // ---- Private helpers ----

    private async Task<(decimal Price, decimal ChangePct)> GetCryptoQuoteAsync(string coinGeckoId)
    {
        var url = $"https://api.coingecko.com/api/v3/simple/price?ids={coinGeckoId}&vs_currencies=usd&include_24hr_change=true";
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty(coinGeckoId);
        var price = root.GetProperty("usd").GetDecimal();
        var change = root.TryGetProperty("usd_24h_change", out var c) ? c.GetDecimal() : 0m;
        return (price, change);
    }

    private async Task<(decimal Price, decimal ChangePct)> GetYahooQuoteAsync(string ticker)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(ticker)}?interval=1d&range=2d";
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var meta = doc.RootElement.GetProperty("chart").GetProperty("result")[0].GetProperty("meta");
        var price = meta.GetProperty("regularMarketPrice").GetDecimal();
        var prevClose = meta.TryGetProperty("chartPreviousClose", out var pc) ? pc.GetDecimal()
                       : meta.GetProperty("previousClose").GetDecimal();
        var changePct = prevClose == 0 ? 0 : (price - prevClose) / prevClose * 100m;
        return (price, changePct);
    }
}
