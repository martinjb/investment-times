# Investment Tracker

A full-stack personal investment tracker built with **.NET 8**, **Angular 17**, and **Entity Framework Core**. It pulls live market data, lets you record buys and sells, calculates your portfolio's gains in real time, and aggregates headlines from the Financial Times and AP News.

The project is intentionally written for a developer learning the stack: the code is clean, the patterns are conventional, and every file has comments explaining *why* it exists in addition to *what* it does.

> See [`ARCHITECTURE.md`](./ARCHITECTURE.md) for a detailed walk-through of the project structure, design patterns, and the request lifecycle.

---

## Features

- **Live market dashboard** — current price + 24h change for Bitcoin, the S&P 500, the Dow Jones, and Brent Crude.
- **Buy / sell recording** — log transactions for any stock ticker (e.g. `AAPL`) or cryptocurrency (e.g. `bitcoin`).
- **Portfolio P/L** — weighted-average cost basis, market value, unrealized gain, and realized gain are recalculated on every change.
- **Live holdings ledger** — see exactly what you own, what you paid, and what it's worth right now.
- **News feed** — latest headlines from the Financial Times and AP News, side-by-side.

## Tech Stack

| Layer       | Tech                                                               |
|-------------|--------------------------------------------------------------------|
| Backend     | ASP.NET Core 8 Web API, C#                                         |
| Persistence | Entity Framework Core 8 + SQLite (zero-config local file)          |
| Frontend    | Angular 17 (standalone components), TypeScript, RxJS               |
| External    | CoinGecko API, Yahoo Finance, FT RSS, AP News RSS                  |

No API keys required. Everything runs locally out of the box.

---

## Prerequisites

Install these once:

1. **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>
2. **Node.js 18+ and npm** — <https://nodejs.org/>
3. **Angular CLI** (global) — `npm install -g @angular/cli`

Verify with:

```bash
dotnet --version    # should print 8.x.x
node --version      # should print v18.x or higher
ng version          # should show Angular CLI 17.x
```

---

## Run It Locally

The app is two processes: a .NET API on port `5000` and an Angular dev server on port `4200`.

### 1. Start the backend

```bash
cd backend/InvestmentTracker.Api
dotnet restore
dotnet run
```

The API is now live at `http://localhost:5000`. Open `http://localhost:5000/swagger` in a browser to explore the endpoints. SQLite will create an `investments.db` file in this folder on first run.

### 2. Start the frontend (in a second terminal)

```bash
cd frontend
npm install
npm start
```

Your browser opens automatically at `http://localhost:4200`.

That's it. Add a buy or two and watch the holdings table populate.

---

## Running the Unit Tests

The backend ships with an xUnit test project covering the cost-basis and P/L math:

```bash
cd backend
dotnet test
```

You should see ~18 passing tests covering single buys, weighted-average cost from multiple buys, partial sells, full sells (closed positions), realized vs unrealized gains, edge cases (zero price, empty portfolio), and grouping by asset type.

The tests use **xUnit** as the runner, **Moq** to stub the external market-data service, **EF Core's InMemory provider** as a fake database, and **FluentAssertions** for readable assertions like `result.Should().Be(...)`. See `backend/InvestmentTracker.Tests/PortfolioServiceTests.cs`.

---

## API Endpoints

| Method | Route                                  | Description                          |
|--------|----------------------------------------|--------------------------------------|
| GET    | `/api/market/indicators`               | BTC, S&P, Dow, Brent live prices     |
| GET    | `/api/portfolio/summary`               | Aggregate P/L numbers                |
| GET    | `/api/portfolio/holdings`              | Per-asset positions with current P/L |
| GET    | `/api/portfolio/transactions`          | Full ledger                          |
| POST   | `/api/portfolio/transactions`          | Record a buy or sell                 |
| DELETE | `/api/portfolio/transactions/{id}`     | Delete a transaction                 |
| GET    | `/api/news`                            | Latest FT + AP headlines             |

Sample POST body:

```json
{
  "symbol": "AAPL",
  "assetType": 0,
  "type": 0,
  "quantity": 10,
  "pricePerUnit": 178.50
}
```

`assetType`: `0` = Stock, `1` = Crypto. `type`: `0` = Buy, `1` = Sell.

---

## Notes for Your Resume / Interview

This project is a compact demonstration of:

- **Clean layered architecture** — controllers → services → data access, with interfaces between layers.
- **Dependency injection** — both .NET's built-in container and Angular's `@Injectable` services.
- **Async/await everywhere** in the .NET code, with proper use of `Task<T>`.
- **Entity Framework Core code-first** with `DbContext` and `DbSet<T>`.
- **DTOs separated from entities** to decouple HTTP contracts from the database schema.
- **Standalone Angular components** (the modern API, replacing NgModules).
- **Reactive state with RxJS** (`BehaviorSubject` to trigger refreshes app-wide).
- **CORS configuration** between two services running on different ports.
- **Integration with three different external APIs** (REST + RSS).

For talking points during an interview, see the "Talking Points" section at the end of `ARCHITECTURE.md`.

---

## Troubleshooting

- **`dotnet run` fails with "SDK not found"** — install the .NET 8 SDK (the runtime alone is not enough).
- **Frontend shows "Loading market data…" forever** — make sure the backend is running on port 5000 and check the browser console for CORS errors.
- **A crypto symbol doesn't return a price** — use the lowercase CoinGecko id (e.g. `bitcoin`, not `BTC`). The full list is at <https://api.coingecko.com/api/v3/coins/list>.
- **AP News headlines are empty** — the RSS feed used (`rsshub.app/apnews/topics/apf-topnews`) is a community-run mirror and occasionally has downtime. Swap it for any other AP RSS URL in `Services/NewsService.cs`.

---

## License

MIT — use it freely on your resume.
