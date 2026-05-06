// Services/NewsService.cs
// Pulls headlines from public RSS feeds (FT and AP).
//
// RSS is a simple XML format. We use System.Xml.Linq (XDocument) to parse it - it's built into .NET.
//
// We're not "scraping" anything; RSS is the publishers' explicitly-published headline feed.

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

    // FT's free "home" RSS feed and AP News' top stories RSS.
    // If either changes URL, only this list needs updating.
    private static readonly (string Source, string Url)[] Feeds =
    {
        ("Financial Times", "https://www.ft.com/rss/home"),
        ("AP News",         "https://rsshub.app/apnews/topics/apf-topnews")
    };

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

                // Standard RSS 2.0 path: rss > channel > item
                var feedItems = doc.Descendants("item").Take(8);

                foreach (var item in feedItems)
                {
                    var title = item.Element("title")?.Value ?? "";
                    var link = item.Element("link")?.Value ?? "";
                    var pubDateStr = item.Element("pubDate")?.Value;

                    DateTime? pubDate = null;
                    if (DateTime.TryParse(pubDateStr, out var d)) pubDate = d.ToUniversalTime();

                    if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                        items.Add(new NewsItemDto(title.Trim(), source, link.Trim(), pubDate));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch news from {Source}", source);
            }
        }

        // Newest first, with feeds that have no publish date going to the end.
        return items.OrderByDescending(n => n.PublishedAt ?? DateTime.MinValue).ToList();
    }
}
