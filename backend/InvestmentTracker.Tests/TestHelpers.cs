// TestHelpers.cs
// Shared helpers for the test project.
//
// EF Core's "InMemory" provider gives us a fake database that lives only for the lifetime
// of a test. No SQLite file, no connection strings, no setup. Each test gets a fresh DB
// by passing a unique name.

using InvestmentTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Tests;

internal static class TestHelpers
{
    public static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            // Unique name per call -> each test is isolated
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
