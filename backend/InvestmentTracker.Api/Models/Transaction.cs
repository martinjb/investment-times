// Models/Transaction.cs
// A single buy or sell event. This is what gets saved to the database.
//
// Design note: this is what's often called a "POCO" (Plain Old CLR Object) - just data, no behavior.
// Entity Framework Core maps each property to a column in the SQLite "Transactions" table automatically.

namespace InvestmentTracker.Api.Models;

public enum TransactionType
{
    Buy = 0,
    Sell = 1
}

public enum AssetType
{
    Stock = 0,
    Crypto = 1
}

public class Transaction
{
    // Primary key. EF Core auto-detects "Id" and makes it auto-incrementing.
    public int Id { get; set; }

    // e.g. "AAPL", "MSFT" for stocks; "bitcoin", "ethereum" for crypto (CoinGecko IDs).
    public string Symbol { get; set; } = string.Empty;

    public AssetType AssetType { get; set; }

    public TransactionType Type { get; set; }

    // How many shares / coins.
    public decimal Quantity { get; set; }

    // Price per unit at the time of the transaction.
    public decimal PricePerUnit { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;
}
