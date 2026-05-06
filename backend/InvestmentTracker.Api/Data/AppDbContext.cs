// Data/AppDbContext.cs
// The DbContext is Entity Framework's bridge between C# objects and the SQLite database.
// Each DbSet<T> property becomes a table.

using InvestmentTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InvestmentTracker.Api.Data;

public class AppDbContext : DbContext
{
    // The constructor parameter is supplied by the DI container - see Program.cs.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Maps to the "Transactions" table.
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // OnModelCreating is where you fine-tune the schema (column types, indexes, etc.).
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            // SQLite has no native decimal type. We store as TEXT to preserve precision.
            entity.Property(t => t.Quantity).HasConversion<string>();
            entity.Property(t => t.PricePerUnit).HasConversion<string>();

            // Index on Symbol so portfolio queries (group-by symbol) stay fast.
            entity.HasIndex(t => t.Symbol);
        });
    }
}
