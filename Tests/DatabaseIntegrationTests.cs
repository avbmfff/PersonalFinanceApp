using Domain.Entities;
using Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class DatabaseIntegrationTests
{
    [Fact]
    public async Task Database_Migrations_ApplySuccessfully()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(conn)
            .Options;

        using var ctx = new FinanceDbContext(options);
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();

        Assert.True(await ctx.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Database_SeedData_Works()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(conn)
            .Options;

        using var ctx = new FinanceDbContext(options);
        await ctx.Database.MigrateAsync();

        // seed
        ctx.Wallets.Add(new Wallet
        {
            Id = Guid.NewGuid(),
            Name = "SeedWallet",
            Currency = "USD",
            InitialBalance = 100m
        });
        await ctx.SaveChangesAsync();

        var wallet = await ctx.Wallets.FirstOrDefaultAsync(w => w.Name == "SeedWallet");
        Assert.NotNull(wallet);
    }
}