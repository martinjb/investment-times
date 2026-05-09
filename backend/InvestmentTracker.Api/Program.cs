// Program.cs
// This is the entry point for our .NET 8 Web API.
// It uses the "minimal hosting" model introduced in .NET 6.
// Everything in this file runs ONCE at startup to wire up the application.

using InvestmentTracker.Api.Data;
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
