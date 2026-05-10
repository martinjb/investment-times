# Architecture & Design Patterns

This document is a guided tour of the codebase. The goal is for you to be able to point at any file in the project and explain *why* it exists, what pattern it implements, and how it talks to its neighbors. It's organized so you can read it linearly or jump to the section you care about.

---

## 1. Project layout at a glance

```
InvestmentTracker/
├── backend/
│   ├── InvestmentTracker.sln        ← Solution file (links the two projects)
│   ├── InvestmentTracker.Api/
│   │   ├── Controllers/             ← HTTP endpoints (thin)
│   │   │   ├── MarketController.cs
│   │   │   ├── NewsController.cs
│   │   │   └── PortfolioController.cs
│   │   ├── Services/                ← Business logic (the brains)
│   │   │   ├── MarketDataService.cs
│   │   │   ├── NewsService.cs
│   │   │   └── PortfolioService.cs
│   │   ├── Data/
│   │   │   └── AppDbContext.cs      ← Entity Framework Core context
│   │   ├── Models/
│   │   │   ├── Transaction.cs       ← Database entity
│   │   │   └── Dtos.cs              ← API request/response shapes
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── Program.cs               ← Application entry point
│   │   ├── appsettings.json
│   │   └── InvestmentTracker.Api.csproj
│   └── InvestmentTracker.Tests/     ← xUnit test project
│       ├── PortfolioServiceTests.cs ← Cost-basis & P/L math tests
│       ├── TestHelpers.cs           ← Shared in-memory DB factory
│       └── InvestmentTracker.Tests.csproj
└── frontend/
    └── src/
        ├── app/
        │   ├── components/          ← UI building blocks
        │   │   ├── dashboard/       ← Landing page (markets + summary)
        │   │   ├── transactions/    ← Buy/sell form + ledger
        │   │   ├── portfolio/       ← Current holdings table
        │   │   └── news/            ← FT + AP feed
        │   ├── services/
        │   │   └── api.service.ts   ← All HTTP calls live here
        │   ├── models/
        │   │   └── models.ts        ← TypeScript interfaces
        │   ├── app.component.ts     ← Root component
        │   ├── app.component.html
        │   └── app.component.css
        ├── styles/
        │   └── global.css           ← Design tokens + base styles
        ├── index.html
        └── main.ts                  ← Angular bootstrap
```

---

## 2. The big picture

```
[Browser]
   │
   │ HTTP (port 4200)
   ▼
[Angular dev server]
   │
   │ HTTP requests via ApiService
   ▼
[ASP.NET Core API on port 5000]
   │
   ├──► CoinGecko / Yahoo Finance / RSS feeds  (outbound HTTP)
   │
   └──► SQLite file: investments.db            (via EF Core)
```

The frontend is a static SPA. The backend is a stateless REST API. Persistence is a single SQLite file living next to the .NET project. There is no authentication — this is a single-user local-first tool.

---

## 3. Backend (.NET 8) — file by file

### 3.1 `Program.cs` — application bootstrap

This file uses the **minimal hosting model** introduced in .NET 6. Everything that used to be split across `Program.cs` and `Startup.cs` now lives in one place:

1. **Build the service container** — `builder.Services.AddX(...)` registers everything that can be injected.
2. **Build the app** — `var app = builder.Build();`
3. **Configure middleware in order** — CORS → routing → controllers.
4. **Run** — `app.Run();`

The order of `app.UseX(...)` calls matters: they form the request pipeline. CORS must be added before authorization, and authorization before `MapControllers()`.

### 3.2 The "Layered Architecture" pattern

This is the most important pattern in the project. Reading from top to bottom:

```
Controller    →   "I receive the HTTP request and return a response."
   ↓
  Service     →   "I run the business logic."
   ↓
  DbContext   →   "I talk to the database."
```

Each layer **only knows about the layer directly below it**. A controller never queries the database; a service never touches `HttpContext`. Why?

- **Testability** — you can unit-test `PortfolioService.ComputeRealizedGain` without spinning up a web server.
- **Replaceability** — if you switch from SQLite to PostgreSQL, only the data layer changes.
- **Clarity** — when something breaks, the bug is in *one* layer. You don't have to grep across the whole codebase.

### 3.3 Controllers (`Controllers/*.cs`)

Each controller is **thin by design**. Look at `PortfolioController.AddTransaction`:

```csharp
[HttpPost("transactions")]
public async Task<IActionResult> AddTransaction([FromBody] CreateTransactionDto dto)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);
    var tx = await _portfolio.AddTransactionAsync(dto);
    return CreatedAtAction(nameof(GetTransactions), new { id = tx.Id }, tx);
}
```

It does three things: validate input, call a service, return an HTTP result. That's it. If you find yourself writing a `for` loop or a calculation in a controller, it belongs in a service.

The `[ApiController]` attribute and `[Route("api/[controller]")]` give us:
- Automatic model-binding from JSON request bodies.
- Automatic 400 responses for validation errors.
- Routing based on the controller class name (`PortfolioController` → `/api/portfolio`).

### 3.4 Services (`Services/*.cs`) — the Service Layer pattern

Every service follows the same shape:

```csharp
public interface IPortfolioService { /* methods */ }
public class PortfolioService : IPortfolioService { /* implementation */ }
```

Why the interface? Because controllers depend on **`IPortfolioService`**, not the concrete class. In `Program.cs`:

```csharp
builder.Services.AddScoped<IPortfolioService, PortfolioService>();
```

This registration tells the DI container "whenever someone asks for `IPortfolioService`, give them a `PortfolioService`." For tests you swap that line and the rest of the app is none the wiser. This is **dependency inversion** — the high-level controller doesn't depend on a low-level class; both depend on an abstraction.

The three services in this project:

- **`MarketDataService`** — encapsulates the two external price APIs (CoinGecko, Yahoo Finance). Includes a tiny in-memory price cache so we don't hit the public APIs on every page render.
- **`NewsService`** — fetches and parses RSS feeds from FT and AP using `XDocument`.
- **`PortfolioService`** — the heart of the app. Computes the weighted-average cost basis, current market value, unrealized P/L, and realized gain. **All the math lives here.**

### 3.5 The Repository-ish layer

In a larger app you might add a `IRepository<Transaction>` between the service and the `DbContext`. For this project, the `DbContext` *is* the repository — `_db.Transactions` is already an abstraction over SQL. Adding another layer would be ceremony without benefit at this scale, but if you grow this app, that's where it would go.

### 3.6 Models — Entities vs DTOs

There are **two kinds of objects** that look similar but serve different roles:

| Entity (`Transaction.cs`)             | DTO (`CreateTransactionDto`, etc.)         |
|---------------------------------------|--------------------------------------------|
| Lives in the database                 | Lives on the wire (HTTP)                   |
| Mapped by Entity Framework            | Serialized by JSON                         |
| Has all internal fields               | Has only what the client needs             |
| Should rarely change shape            | Can change with API versions               |

In this project they happen to overlap a lot, but the *separation is what matters*. The day you add `UserId` to `Transaction`, you don't want every external client to suddenly receive that field — the DTO acts as a deliberate filter.

The `record` keyword (C# 9+) makes DTOs concise:

```csharp
public record HoldingDto(string Symbol, decimal Quantity, ...);
```

A `record` is an immutable class with value-equality. Perfect for DTOs because DTOs should be treated as immutable snapshots.

### 3.7 Entity Framework Core

`AppDbContext` is the bridge between C# and the database. The pattern:

1. Define a class (`Transaction`).
2. Add it to the `DbContext` as `DbSet<Transaction> Transactions`.
3. EF generates the SQL.

`db.Database.EnsureCreated()` in `Program.cs` creates the SQLite file and tables on first run. For a real app you'd use **migrations** (`dotnet ef migrations add Initial`) so schema changes are versioned, but `EnsureCreated` is fine for learning.

The `OnModelCreating` override stores `decimal` values as `string` in SQLite. SQLite has no native `decimal` type, and storing as `string` preserves precision exactly — important for money.

### 3.8 Dependency Injection — how it actually flows

When a request hits `POST /api/portfolio/transactions`:

1. ASP.NET Core asks the DI container for a `PortfolioController`.
2. The container sees the controller's constructor needs `IPortfolioService`.
3. It looks up the registration and creates a `PortfolioService`.
4. `PortfolioService`'s constructor needs `AppDbContext` and `IMarketDataService`.
5. Container creates those, recursively, until everything is satisfied.

You never `new` anything. You just declare what you need in your constructor and the container hands it to you. **`AddScoped`** means "one instance per HTTP request" — the right choice for things that hold per-request state like a `DbContext`.

### 3.9 CORS

The browser refuses to let JavaScript from `localhost:4200` call an API on `localhost:5000` unless the API explicitly allows it. The named policy in `Program.cs`:

```csharp
options.AddPolicy("AllowAngular", policy =>
    policy.WithOrigins("http://localhost:4200")
          .AllowAnyHeader()
          .AllowAnyMethod());
```

…and `app.UseCors("AllowAngular")` activates it.

---

## 4. Frontend (Angular 17) — file by file

### 4.1 The "standalone components" approach

This project does **not** use `NgModule`. Angular 14 introduced standalone components, and Angular 17 made them the default. Each component declares its own dependencies:

```typescript
@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
```

`bootstrapApplication(AppComponent, { providers: [provideHttpClient()] })` in `main.ts` replaces the old `AppModule` ceremony entirely. Less boilerplate, and the imports for each component are right at the top of the file where they're used.

### 4.2 Component anatomy

Every component is three files:

- `*.component.ts` — class with state and methods.
- `*.component.html` — template with bindings (`{{ value }}`, `*ngFor`, etc.).
- `*.component.css` — scoped styles (Angular automatically scopes them to this component).

The components in this app:

| Component               | Responsibility                                          |
|-------------------------|---------------------------------------------------------|
| `AppComponent`          | Layout shell — masthead, content area, footer.          |
| `DashboardComponent`    | Four market tickers + portfolio summary cards.          |
| `TransactionsComponent` | Buy/sell form + recent activity ledger.                 |
| `PortfolioComponent`    | Holdings table with cost basis and current P/L.         |
| `NewsComponent`         | Two-column FT + AP headline feed.                       |

### 4.3 The single `ApiService`

Every HTTP call in the app goes through `services/api.service.ts`. This is the same idea as the .NET service layer — components don't talk to the network directly.

The `@Injectable({ providedIn: 'root' })` decorator means there's exactly **one** instance of `ApiService` shared across the whole app. That makes it the natural place to hold app-wide state, like the refresh signal:

```typescript
private readonly _refresh$ = new BehaviorSubject<number>(0);
readonly refresh$ = this._refresh$.asObservable();

addTransaction(tx: CreateTransaction) {
  return this.http.post<Transaction>(...).pipe(
    tap(() => this.triggerRefresh())  // notify everyone
  );
}
```

When `TransactionsComponent` adds a new buy, `triggerRefresh()` fires. `DashboardComponent` and `PortfolioComponent` are subscribed to `refresh$` and reload themselves. **No prop-drilling, no event emitters bouncing up and down a component tree.** This is the classic RxJS observable-as-event-bus pattern.

### 4.4 Reactive programming with RxJS

Angular's `HttpClient` returns `Observable<T>`, not `Promise<T>`. The mental model:

- A `Promise` is a one-shot value.
- An `Observable` is a stream you subscribe to.

For a one-off API call, the difference doesn't matter much:

```typescript
this.api.getHoldings().subscribe(data => this.holdings = data);
```

But the Observable model lets you do things like `tap()`, `map()`, `filter()`, and combine streams — perfect for the refresh-broadcast pattern.

**Important:** when a component is destroyed, you should `unsubscribe()` to avoid memory leaks. That's what the `OnDestroy` hooks and `Subscription` field in each component do:

```typescript
ngOnInit()    { this.sub = this.api.refresh$.subscribe(...); }
ngOnDestroy() { this.sub?.unsubscribe(); }
```

### 4.5 Forms — Template-driven via `ngModel`

The transaction form uses **template-driven forms** (`[(ngModel)]`) rather than reactive forms. Trade-off:

- **Template-driven** — simpler, more declarative, fewer files. Best for small forms.
- **Reactive forms** — more powerful, easier to validate complex rules, easier to unit-test. Best for large or dynamic forms.

For a 5-field form, template-driven wins on readability. The `[(ngModel)]="form.symbol"` syntax is two-way binding: typing in the input updates `form.symbol`, and changing `form.symbol` in code updates the input.

### 4.6 TypeScript models

`models/models.ts` mirrors the C# DTOs exactly. Keeping the contract definitions in one file makes it obvious when the frontend and backend drift. In a real app you'd auto-generate this from the OpenAPI/Swagger schema using something like NSwag — but for learning, hand-written interfaces make the shape crystal clear.

### 4.7 CSS and design system

`styles/global.css` defines **design tokens** as CSS custom properties:

```css
:root {
  --paper:  #f4ece1;
  --ink:    #161514;
  --gain:   #1f6b3a;
  --serif:  'Fraunces', serif;
  --mono:   'JetBrains Mono', monospace;
}
```

Every component CSS file references these variables instead of hardcoding colors and fonts. Change one variable, the whole app updates.

The aesthetic is "editorial finance press": cream paper background like the FT's iconic salmon-pink, a high-contrast serif (Fraunces) for headlines, JetBrains Mono for numbers, and Inter for UI text. The masthead uses the typography conventions of a real newspaper — issue number, date, drop-cap "The". This is intentional differentiation: most CRUD apps look like Bootstrap dashboards.

Component-level CSS is **scoped automatically by Angular**. You can write `.holding-row` in two different components without collision.

---

## 5. Request lifecycles

### 5.1 What happens when you click "Record Buy"

1. **`TransactionsComponent.submit()`** validates the form locally.
2. It calls `this.api.addTransaction(this.form)`.
3. **`ApiService`** sends `POST http://localhost:5000/api/portfolio/transactions` with a JSON body.
4. ASP.NET Core's CORS middleware allows the request (origin matches).
5. ASP.NET Core constructs `PortfolioController` from the DI container.
6. `[FromBody] CreateTransactionDto` deserializes the JSON.
7. Controller calls `_portfolio.AddTransactionAsync(dto)`.
8. **`PortfolioService`** normalizes the symbol, builds a `Transaction` entity, calls `_db.SaveChangesAsync()`.
9. EF Core generates an `INSERT` statement and writes to `investments.db`.
10. The new entity is returned up the chain. Controller returns `201 Created`.
11. **Back in the browser:** `ApiService.addTransaction` succeeds and fires `triggerRefresh()`.
12. `DashboardComponent` and `PortfolioComponent` (subscribed to `refresh$`) re-fetch their data.
13. The summary cards and holdings table re-render with the new numbers.

### 5.2 What happens when the dashboard loads

1. `DashboardComponent.ngOnInit()` calls `this.api.getIndicators()`.
2. ASP.NET Core routes to `MarketController.GetIndicators`.
3. Controller calls `_market.GetLandingIndicatorsAsync()`.
4. `MarketDataService` makes four sequential HTTP calls — one to CoinGecko for BTC, then three to Yahoo Finance for ^GSPC, ^DJI, BZ=F.
5. Each result is deserialized into a `MarketIndicatorDto`.
6. The list comes back through the controller as JSON.
7. The Angular component populates `this.indicators` and the template re-renders.

---

## 6. Patterns reference card

| Pattern                     | Where you'll find it                              |
|-----------------------------|---------------------------------------------------|
| Layered Architecture        | `Controllers` → `Services` → `Data` (.NET)        |
| Dependency Injection        | Constructor injection in every C# class           |
| Service Layer               | `Services/*.cs` and `services/api.service.ts`     |
| Repository (informal)       | `AppDbContext.Transactions` (`DbSet<T>`)          |
| DTO                         | `Models/Dtos.cs`                                  |
| Entity / Domain object      | `Models/Transaction.cs`                           |
| Standalone components       | Every `@Component` in `frontend/src/app`          |
| Observable broadcast bus    | `ApiService.refresh$`                             |
| Two-way binding             | `[(ngModel)]` in transaction form                 |
| Template-driven form        | `transactions.component.html`                     |
| Design tokens               | `styles/global.css`                               |
| CORS                        | `Program.cs`                                      |
| AAA testing                 | `PortfolioServiceTests.cs`                        |
| Mocking dependencies        | `Moq` + `IMarketDataService` stub in tests        |
| In-memory test database     | `TestHelpers.CreateInMemoryDb()`                  |
| Table-driven tests          | `[Theory] + [InlineData]` in tests                |

---

## 7. Testing patterns

The test project (`backend/InvestmentTracker.Tests`) targets `PortfolioService` because that's where the actual business logic lives. The other services are thin wrappers around external HTTP calls — testing them well means hitting real APIs (which is integration testing, not unit testing).

The tests demonstrate four patterns worth knowing:

**Arrange-Act-Assert (AAA)** — every test has the same three-part shape: build the inputs, run the operation under test, check the outcome. Keeping that order rigid makes tests readable in seconds.

**Mocking with Moq** — `PortfolioService` depends on `IMarketDataService`. Using a mock means tests don't need the internet:

```csharp
var marketMock = new Mock<IMarketDataService>();
marketMock.Setup(m => m.GetPricesAsync(It.IsAny<...>()))
          .ReturnsAsync(fixedPrices);
```

This is exactly why the service interfaces exist: to make this swap trivial.

**EF Core InMemory provider** — instead of mocking `DbContext` (painful, doesn't exercise real EF behaviour), we use the InMemory provider:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
return new AppDbContext(options);
```

A unique database name per test means tests don't pollute each other.

**Theory tests for table-driven cases** — when you want to run the same logic against many input/output pairs:

```csharp
[Theory]
[InlineData(100, 100, 0)]
[InlineData(100, 200, 100)]
[InlineData(100, 50, -50)]
public async Task GainPercent_CalculatedCorrectly(decimal buy, decimal current, decimal expectedPct) { ... }
```

xUnit runs the method once per `[InlineData]`, and each row shows up as its own test in the runner.

**FluentAssertions** — `result.Should().Be(150m)` reads better in failure messages than `Assert.Equal(150m, result)`. Worth the dependency.

---

## 8. Talking points for an interview

A few things that make for good 60-second answers:

**"Walk me through the structure."** Layered backend (controllers, services, EF data context). Angular frontend with standalone components and a single shared API service. Communication via REST + JSON. Persistence in SQLite via EF Core. Refresh propagation via an RxJS BehaviorSubject.

**"Why DTOs separate from entities?"** So the database schema and the public API can evolve independently. If I add an internal field to `Transaction`, no clients see it unless I add it to the DTO too. Also lets me use `record` types and validation attributes targeted at HTTP, not at the DB layer.

**"Why do you use interfaces for services?"** Two reasons: tests (I can swap in a fake implementation in seconds) and the open/closed principle (future implementations don't change the consumer). It's the dependency-inversion principle from SOLID.

**"How does the dashboard know to refresh after a buy?"** The single `ApiService` exposes a `BehaviorSubject` called `refresh$`. On a successful POST, the service emits a value. Components that care subscribe in `ngOnInit` and reload themselves. It's an in-app event bus that costs about three lines of code.

**"Why SQLite?"** Zero configuration, single file, works on every OS, plenty fast for a single-user app. Same EF Core code runs against PostgreSQL or SQL Server with one line changed in `Program.cs`.

**"What would you do differently in production?"** Add real EF migrations instead of `EnsureCreated`. Authentication (e.g. via OIDC). A real cache layer (Redis) for prices. Push live prices via SignalR rather than polling. Move secrets out of `appsettings.json`. Add a CI pipeline. Probably containerize with Docker.

---

That's the whole project. Once you've read this once and poked at the code for an afternoon, you should be able to describe every piece confidently.
