using Application.Interfaces;
using Application.Services;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, builder) => { builder.AddJsonFile("appsettings.json", optional: true); })
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;
        var connectionString = configuration.GetConnectionString("FinanceSqlite") ?? "Data Source=finance.db";

        services.AddDbContext<FinanceDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IFinanceService, FinanceService>();
    })
    .Build();

await RunConsoleAsync(host.Services);

static async Task RunConsoleAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var financeService = scope.ServiceProvider.GetRequiredService<IFinanceService>();
    var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

    Console.WriteLine("Введите месяц для отчёта в формате YYYY-MM (например, 2025-10):");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input) || !TryParseYearMonth(input, out var year, out var month))
    {
        Console.WriteLine("Неверный формат. Завершение.");
        return;
    }

    try
    {
        var report = await financeService.GetMonthlyTransactionsGroupedByTypeAsync(year, month);
        Console.WriteLine($"Отчёт за {year:D4}-{month:D2}");

        foreach (var group in report.Groups)
        {
            Console.WriteLine($"--- {group.Type} | Всего: {group.TotalAmount}");
            foreach (var tx in group.Transactions)
            {
                Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Amount,10} | {tx.Description}");
            }
        }

        var topPerWallet = await financeService.GetTopExpensesPerWalletAsync(year, month, 3);
        Console.WriteLine();
        Console.WriteLine("Топ 3 траты по каждому кошельку за месяц:");
        foreach (var kv in topPerWallet)
        {
            var walletId = kv.Key;
            var transactions = kv.Value;

            var wallet = await dbContext.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.Id == walletId);
            var walletName = wallet?.Name ?? walletId.ToString();

            Console.WriteLine($"Кошелёк: {walletName}");
            foreach (var tx in transactions)
            {
                Console.WriteLine($"{tx.Date:yyyy-MM-dd} | {tx.Amount,10} | {tx.Description}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
    }
}

static bool TryParseYearMonth(string input, out int year, out int month)
{
    year = 0;
    month = 0;
    var parts = input.Split('-', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2) return false;
    if (!int.TryParse(parts[0], out year) || !int.TryParse(parts[1], out month)) return false;
    return month >= 1 && month <= 12;
}