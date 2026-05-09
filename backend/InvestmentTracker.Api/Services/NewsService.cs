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
    private static readonly (string Source, string Url)[] Feeds =
    {
        ("Yahoo Finance",   "https://finance.yahoo.com/news/rssindex"),
        ("Seeking Alpha",   "https://seekingalpha.com/tag/wall-st-breakfast.xml"),
        ("BBC News",        "https://feeds.bbci.co.uk/news/world/rss.xml"),
        ("Financial Times", "https://www.ft.com/rss/home")
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

        for (var i = 0; i < Feeds.Length; i++)
        {
            var (source, url) = Feeds[i];
            try
            {
                var xml = await _http.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                // Sort each feed's articles newest-first, then append in feed order.
                // This keeps the source columns in the order defined by Feeds[].
                var feedItems = ParseFeed(doc, source)
                    .OrderByDescending(n => n.PublishedAt ?? DateTime.MinValue);
                items.AddRange(feedItems);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch news from {Source}", source);
            }
        }

        return items;
    }

    // Parses both RSS 2.0 (<item>) and Atom 1.0 (<entry>) feeds.
    private static IEnumerable<NewsItemDto