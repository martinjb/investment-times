// Program.cs
// This is the entry point for our .NET 8 Web API.
// It uses the "minimal hosting" model introduced in .NET 6.
// Everything in this file runs ONCE at startup to wire up the application.

using InvestmentTracker.Api.Data;
using InvestmentTracker.Api.Models;
using InvestmentTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ----- 1. Register Services (Dependency Injection container) -----
// Every "AddXxx" call below registers something the rest of the app can request via constructor injection.

// MVC Controllers (the classes in /Controllers).
builder.Services.AddControllers();

// Swagger / OpenAPI for testing the API in the browser at /swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework Core with SQLite.
// The connection string points to a local file "investments.db" which is created on first run.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=investments.db"));

// HttpClient factory used by services that call external APIs (CoinGecko, Yahoo Finance, etc.).
builder.Services.AddHttpClient();

// Our own services. AddScoped = a new instance per HTTP request, which is the standard choice for web apps.
builder.Services.AddScoped<IMarketDataService, MarketDataService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IPortfolioService, PortfolioService>();

// CORS - allow the Angular dev server (port 4200) to call this API (port 5000).
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ----- 2. Configure the HTTP pipeline (the order matters) -----

// Make sure the database exists and has the right schema on startup.
// For a learning project, EnsureCreated is fine. For production, use real EF migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Seed demo transactions so the Holdings, Portfolio and Transactions sections
    // are populated on first run. Only runs once — skipped if any transactions exist.
    if (!db.Transactions.Any())
    {
        var seed = new List<Transaction>
        {
            // ── Stocks ──────────────────────────────────────────────────────────
            new() { Symbol = "AAPL",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 20,   PricePerUnit = 162.50m,  Date = DateTime.UtcNow.AddDays(-120) },
            new() { Symbol = "AAPL",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 10,   PricePerUnit = 175.00m,  Date = DateTime.UtcNow.AddDays(-60)  },
            new() { Symbol = "AAPL",  AssetType = AssetType.Stock,  Type = TransactionType.Sell, Quantity = 8,    PricePerUnit = 189.00m,  Date = DateTime.UtcNow.AddDays(-10)  },

            new() { Symbol = "MSFT",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 15,   PricePerUnit = 310.00m,  Date = DateTime.UtcNow.AddDays(-150) },
            new() { Symbol = "MSFT",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 5,    PricePerUnit = 390.00m,  Date = DateTime.UtcNow.AddDays(-30)  },

            new() { Symbol = "NVDA",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 12,   PricePerUnit = 480.00m,  Date = DateTime.UtcNow.AddDays(-200) },
            new() { Symbol = "NVDA",  AssetType = AssetType.Stock,  Type = TransactionType.Sell, Quantity = 4,    PricePerUnit = 820.00m,  Date = DateTime.UtcNow.AddDays(-45)  },

            new() { Symbol = "GOOGL", AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 8,    PricePerUnit = 138.00m,  Date = DateTime.UtcNow.AddDays(-90)  },

            new() { Symbol = "AMZN",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 10,   PricePerUnit = 178.00m,  Date = DateTime.UtcNow.AddDays(-75)  },
            new() { Symbol = "AMZN",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 5,    PricePerUnit = 192.00m,  Date = DateTime.UtcNow.AddDays(-20)  },

            new() { Symbol = "TSLA",  AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 25,   PricePerUnit = 210.00m,  Date = DateTime.UtcNow.AddDays(-180) },
            new() { Symbol = "TSLA",  AssetType = AssetType.Stock,  Type = TransactionType.Sell, Quantity = 10,   PricePerUnit = 260.00m,  Date = DateTime.UtcNow.AddDays(-50)  },

            new() { Symbol = "JPM",   AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 18,   PricePerUnit = 155.00m,  Date = DateTime.UtcNow.AddDays(-110) },

            new() { Symbol = "XOM",   AssetType = AssetType.Stock,  Type = TransactionType.Buy,  Quantity = 30,   PricePerUnit = 105.00m,  Date = DateTime.UtcNow.AddDays(-130) },
            new() { Symbol = "XOM",   AssetType = AssetType.Stock,  Type = TransactionType.Sell, Quantity = 10,   PricePerUnit = 118.00m,  Date = DateTime.UtcNow.AddDays(-40)  },

            // ── Crypto ──────────────────────────────────────────────────────────
            new() { Symbol = "bitcoin",      AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 0.5m,  PricePerUnit = 42000m,   Date = DateTime.UtcNow.AddDays(-160) },
            new() { Symbol = "bitcoin",      AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 0.25m, PricePerUnit = 61000m,   Date = DateTime.UtcNow.AddDays(-35)  },

            new() { Symbol = "ethereum",     AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 4m,    PricePerUnit = 2200m,    Date = DateTime.UtcNow.AddDays(-140) },
            new() { Symbol = "ethereum",     AssetType = AssetType.Crypto, Type = TransactionType.Sell, Quantity = 1m,    PricePerUnit = 3400m,    Date = DateTime.UtcNow.AddDays(-25)  },

            new() { Symbol = "solana",       AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 30m,   PricePerUnit = 95m,      Date = DateTime.UtcNow.AddDays(-100) },
            new() { Symbol = "solana",       AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 20m,   PricePerUnit = 145m,     Date = DateTime.UtcNow.AddDays(-15)  },

            new() { Symbol = "chainlink",    AssetType = AssetType.Crypto, Type = TransactionType.Buy,  Quantity = 150m,  PricePerUnit = 14.50m,   Date = DateTime.UtcNow.AddDays(-80)  },
        };

        db.Transactions.AddRange(seed);
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

app.Run();
