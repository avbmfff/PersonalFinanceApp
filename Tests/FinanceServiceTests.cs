using Application.Exceptions;
using Application.Services;
using Domain.Enums;
using Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class FinanceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<FinanceDbContext> _options;

    public FinanceServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new FinanceDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateWallet_Then_GetBalanceIsInitial()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var dto = await svc.CreateWalletAsync("Test", "USD", 100m);
        Assert.Equal(100m, dto.InitialBalance);

        var balance = await svc.GetCurrentBalanceAsync(dto.Id);
        Assert.Equal(100m, balance);
    }

    [Fact]
    public async Task AddIncome_IncreasesBalance()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var w = await svc.CreateWalletAsync("W", "EUR", 50m);
        await svc.AddTransactionAsync(w.Id, DateTimeOffset.UtcNow, 25m, TransactionType.Income, "Salary");

        var balance = await svc.GetCurrentBalanceAsync(w.Id);
        Assert.Equal(75m, balance);
    }

    [Fact]
    public async Task AddExpense_DecreasesBalance()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var w = await svc.CreateWalletAsync("W2", "EUR", 100m);
        await svc.AddTransactionAsync(w.Id, DateTimeOffset.UtcNow, 30m, TransactionType.Expense, "Groceries");

        var balance = await svc.GetCurrentBalanceAsync(w.Id);
        Assert.Equal(70m, balance);
    }

    [Fact]
    public async Task AddExpense_InsufficientFunds_Throws()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var w = await svc.CreateWalletAsync("W3", "USD", 20m);
        await Assert.ThrowsAsync<InsufficientFundsException>(async () =>
        {
            await svc.AddTransactionAsync(w.Id, DateTimeOffset.UtcNow, 50m, TransactionType.Expense, "BigBuy");
        });
    }

    [Fact]
    public async Task AddTransaction_NegativeAmount_ThrowsInvalidAmount()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var w = await svc.CreateWalletAsync("W4", "USD", 10m);
        await Assert.ThrowsAsync<InvalidAmountException>(async () =>
        {
            await svc.AddTransactionAsync(w.Id, DateTimeOffset.UtcNow, -5m, TransactionType.Income, "Invalid");
        });
    }

    [Fact]
    public async Task MonthlyReport_GroupsByType_And_TopExpenses()
    {
        using var ctx = new FinanceDbContext(_options);
        var svc = new FinanceService(ctx);

        var w1 = await svc.CreateWalletAsync("A", "USD", 100m);
        var w2 = await svc.CreateWalletAsync("B", "USD", 200m);
        var now = DateTimeOffset.UtcNow;
        var month = now.Month;
        var year = now.Year;

        await svc.AddTransactionAsync(w1.Id, now.AddDays(-1), 10m, TransactionType.Expense, "E1");
        await svc.AddTransactionAsync(w1.Id, now.AddDays(-2), 5m, TransactionType.Expense, "E2");
        await svc.AddTransactionAsync(w2.Id, now.AddDays(-3), 50m, TransactionType.Expense, "E3");
        await svc.AddTransactionAsync(w1.Id, now.AddDays(-4), 100m, TransactionType.Income, "Inc");

        var report = await svc.GetMonthlyTransactionsGroupedByTypeAsync(year, month);
        Assert.Contains(report.Groups, g => g.Type == TransactionType.Expense);
        Assert.Contains(report.Groups, g => g.Type == TransactionType.Income);

        var tops = await svc.GetTopExpensesPerWalletAsync(year, month, 2);
        Assert.True(tops.ContainsKey(w1.Id));
        Assert.True(tops.ContainsKey(w2.Id));
        Assert.Equal(2, tops[w1.Id].Count);
    }
}