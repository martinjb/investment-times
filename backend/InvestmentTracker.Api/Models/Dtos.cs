// Models/Dtos.cs
// "DTO" = Data Transfer Object. These are the shapes we send to / receive from the client.
//
// Why separate DTOs from the database "Transaction" entity?
//   - The DB model and the API contract can change independently.
//   - You can hide internal fields (e.g. you'd never expose a password hash).
//   - Validation messages are clearer when DTOs are tailored to one endpoint.
//
// "record" is a C# 9+ shorthand for an immutable class with value-style equality. Great for DTOs.

using System.ComponentModel.DataAnnotations;

namespace InvestmentTracker.Api.Models;

// Request body when the user adds a new transaction.
public record CreateTransactionDto(
    [Required] string Symbol,
    [Required] AssetType AssetType,
    [Required] TransactionType Type,
    [Range(0.0000001, double.MaxValue)] decimal Quantity,
    [Range(0.0000001, double.MaxValue)] decimal PricePerUnit
);

// One row in the "Holdings" view: how much of each asset the user currently owns and how it's performing.
public record HoldingDto(
    string Symbol,
    AssetType AssetType,
    decimal Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal TotalCost,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent
);

// Aggregate numbers for the dashboard cards.
public record PortfolioSummaryDto(
    decimal TotalCost,
    decimal MarketValue,
    decimal UnrealizedGain,
    decimal UnrealizedGainPercent,
    decimal RealizedGain,
    int HoldingCount
);

// One of the four tickers shown on the landing page.
public record MarketIndicatorDto(
    string Name,
    string Symbol,
    decimal Price,
    decimal ChangePercent24h
);

// A news headline.
public record NewsItemDto(
    string Title,
    string Source,
    string Url,
    DateTime? PublishedAt
);
