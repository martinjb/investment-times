// Controllers/NewsController.cs

using InvestmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly INewsService _news;

    public NewsController(INewsService news) => _news = news;

    // GET /api/news -> latest headlines from FT + AP
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _news.GetHeadlinesAsync());
}
