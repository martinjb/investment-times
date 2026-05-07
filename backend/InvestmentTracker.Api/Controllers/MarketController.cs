// Controllers/MarketController.cs
// Thin HTTP layer. The pattern in every action is:
//   1. Receive request
//   2. Call a service method
//   3. Return the result
// No business logic in here.

using InvestmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // route becomes /api/market
public class MarketController : ControllerBase
{
    private readonly IMarketDataService _market;

    public MarketController(IMarketDataService market) => _market = market;

    // GET /api/market/indicators
    [HttpGet("indicators")]
    public async Task<IActionResult> GetIndicators()
    {
        var data = await _market.GetLandingIndicatorsAsync();
        return Ok(data);
    }

    // GET /api/market/trackers
    [HttpGet("trackers")]
    public async Task<IActionResult> GetTrackers()
    {
        var data = await _market.GetPriceTrackersAsync();
        return Ok(data);
    }
}
