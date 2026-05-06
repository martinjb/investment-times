// Controllers/PortfolioController.cs
// Endpoints for transactions, holdings, and the dashboard summary.

using InvestmentTracker.Api.Models;
using InvestmentTracker.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace InvestmentTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolio;

    public PortfolioController(IPortfolioService portfolio) => _portfolio = portfolio;

    // GET /api/portfolio/summary  -> totals for the dashboard cards
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary() => Ok(await _portfolio.GetSummaryAsync());

    // GET /api/portfolio/holdings -> list of currently-held assets with P&L
    [HttpGet("holdings")]
    public async Task<IActionResult> GetHoldings() => Ok(await _portfolio.GetHoldingsAsync());

    // GET /api/portfolio/transactions -> full ledger
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions() => Ok(await _portfolio.GetTransactionsAsync());

    // POST /api/portfolio/transactions -> record a buy or sell
    [HttpPost("transactions")]
    public async Task<IActionResult> AddTransaction([FromBody] CreateTransactionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var tx = await _portfolio.AddTransactionAsync(dto);
        return CreatedAtAction(nameof(GetTransactions), new { id = tx.Id }, tx);
    }

    // DELETE /api/portfolio/transactions/{id}
    [HttpDelete("transactions/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _portfolio.DeleteTransactionAsync(id);
        return NoContent();
    }
}
