// Services/NewsService.cs
// Pulls headlines from public RSS/Atom feeds.
//
// RSS 2.0 uses <item> / <link> / <pubDate>.
// Atom 1.0 uses <entry> / <link href="..."> / <published> or <updated>.
// We handle both so we're not tied to a single feed format.
//
// We're not "scraping" anything; RSS/Atom are the publishers' explicitly-published headline feeds.

using System.Xml.Linq;
using InvestmentTracker.Api.Models;

namespace InvestmentTracker.Api.Services;

public interface INewsService
{
    Task<List<NewsItemDto>> GetHeadlinesAsync();
}

public class NewsService : INewsService
{
    private readonly HttpClient _http;
    private readonly ILogger<NewsService> _logger;

    // RSS/Atom feeds in display order (top-left, top-right, bottom-left, bottom-right).
    // If any URL changes, only this list needs updating.
    //
    // AP News:     direct feed from apnews.com (rsshub mirror was unreliable)
    // Reuters:     Reuters Agency feed — reuters.com killed public RSS in 2020;
    //              reutersagency.com still publishes an official WordPress RSS feed
    private static readonly (string Source, string Url)[] Feeds =
    {
        ("Financial Times", "https://www.ft.com/rss/home"),
        ("BBC News",        "https://feeds.bbci.co.uk/news/world/rss.xml"),
        ("Yahoo Finance",   "https://finance.yahoo.com/news/rssindex"),
        ("MarketWatch",     "https://feeds.marketwatch.com/marketwatch/topstories/")
    };

    // Atom namespace — needed to find <link> elements in Atom feeds
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public NewsService(IHttpClientFactory httpFactory, ILogger<NewsService> logger)
    {
        _http = httpFactory.CreateClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "InvestmentTracker/1.0");
        _logger = logger;
    }

    public async Task<List<NewsItemDto>> GetHeadlinesAsync()
    {
        var items = new List<NewsItemDto>();

        foreach (var (source, url) in Feeds)
        {
            try
            {
                var xml = await _http.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                items.AddRange(ParseFeed(doc, source));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch news from {Source}", source);
            }
        }

        // Newest first, with feeds that have no publish date going to the end.
        return items.OrderByDescending(n => n.PublishedAt ?? DateTime.MinValue).ToList();
    }

    // Parses both RSS 2.0 (<item>) an