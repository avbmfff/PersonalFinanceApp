using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static System.Console;

class Program
{
    static async Task Main(string[] args)
    {
        var dbFile = Path.Combine(AppContext.BaseDirectory, "finance.db");

        if (File.Exists(dbFile))
        {
            File.Delete(dbFile);
            WriteLine("Старая база удалена.");
        }

        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, services) =>
            {
                var connectionString = $"Data Source={dbFile}";
                services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(connectionString));
                services.AddScoped<IFinanceService, FinanceService>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var financeService = scope.ServiceProvider.GetRequiredService<IFinanceService>();

        WriteLine("Применяем миграции...");
        await dbContext.Database.MigrateAsync();
        WriteLine("Миграции применены.");

        WriteLine("Генерируем тестовые данные...");
        GenerateTestData(dbContext);
        WriteLine("Тестовые данные добавлены.\n");

        await RunReportAsync(dbContext, financeService);
    }

    static void GenerateTestData(FinanceDbContext db)
    {
        if (db.Wallets.Any()) return;

        var rnd = new Random();

        var wallets = new List<Wallet>
        {
            new Wallet { Id = Guid.NewGuid(), Name = "Основной", Currency = "USD", InitialBalance = 500m },
            new Wallet { Id = Guid.NewGuid(), Name = "Карта", Currency = "EUR", InitialBalance = 1000m },
            new Wallet { Id = Guid.NewGuid(), Name = "Наличные", Currency = "RUB", InitialBalance = 10000m },
        };

        db.Wallets.AddRange(wallets);
        db.SaveChanges();

        foreach (var wallet in wallets)
        {
            for (int i = 0; i < 50; i++)
            {
                var type = rnd.Next(0, 2) == 0 ? TransactionType.Income : TransactionType.Expense;
                var amount = Math.Round(rnd.NextDouble() * 500 + 10, 2); // от 10 до 510
                var daysAgo = rnd.Next(0, 90);
                var date = DateTime.UtcNow.AddDays(-daysAgo);

                db.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet.Id,
                    Date = date,
                    Amount = (decimal)amount,
                    Type = type,
                    Description = type == TransactionType.Income ? $"Доход #{i}" : $"Расход #{i}"
                });
            }
        }

        db.SaveChanges();
    }

    static async Task RunReportAsync(FinanceDbContext dbContext, IFinanceService financeService)
    {
        WriteLine("=== Финансовый отчет ===");
        WriteLine("Введите месяц для отчета (YYYY-MM):");
        var input = ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input) || !TryParseYearMonth(input, out var year, out var month))
        {
            ForegroundColor = ConsoleColor.Red;
            WriteLine("Неверный формат. Завершение.");
            ResetColor();
            return;
        }

        var wallets = await dbContext.Wallets.AsNoTracking().ToDictionaryAsync(w => w.Id);

        try
        {
            var report = await financeService.GetMonthlyTransactionsGroupedByTypeAsync(year, month);
            var topPerWallet = await financeService.GetTopExpensesPerWalletAsync(year, month, 3);

            ForegroundColor = ConsoleColor.Cyan;
            WriteLine($"\n=== Отчёт за {year:D4}-{month:D2} ===");
            ResetColor();

            foreach (var group in report.Groups)
            {
                string currency = group.Transactions.Count > 0 &&
                                  wallets.TryGetValue(group.Transactions[0].WalletId, out var w)
                    ? w.Currency
                    : "";

                ForegroundColor = group.Type == TransactionType.Income ? ConsoleColor.Green : ConsoleColor.Red;
                WriteLine($"\n--- {group.Type} | Всего: {group.TotalAmount:0.00} {currency} ---");
                ResetColor();

                if (!group.Transactions.Any())
                {
                    WriteLine("Нет транзакций в этой категории.");
                    continue;
                }

                WriteLine($"{"Дата",-12} {"Сумма",10} {"Описание",-30}");
                WriteLine(new string('-', 55));
                foreach (var tx in group.Transactions.OrderBy(t => t.Date))
                {
                    WriteLine($"{tx.Date:yyyy-MM-dd} {tx.Amount,10:0.00} {tx.Description,-30}");
                }
            }

            ForegroundColor = ConsoleColor.Yellow;
            WriteLine("\n=== Топ 3 траты по каждому кошельку ===");
            ResetColor();

            foreach (var kv in topPerWallet)
            {
                var wallet = wallets.TryGetValue(kv.Key, out var w) ? w : null;
                var walletName = wallet?.Name ?? kv.Key.ToString();
                var currency = wallet?.Currency ?? "";

                ForegroundColor = ConsoleColor.Magenta;
                WriteLine($"\nКошелёк: {walletName} ({currency})");
                ResetColor();

                if (!kv.Value.Any())
                {
                    WriteLine("Нет расходов за месяц.");
                    continue;
                }

                WriteLine($"{"Дата",-12} {"Сумма",10} {"Описание",-30}");
                WriteLine(new string('-', 55));
                foreach (var tx in kv.Value.OrderByDescending(t => t.Amount))
                {
                    WriteLine($"{tx.Date:yyyy-MM-dd} {tx.Amount,10:0.00} {tx.Description,-30}");
                }
            }

            ForegroundColor = ConsoleColor.Cyan;
            WriteLine("\n=== Конец отчета ===");
            ResetColor();
        }
        catch (Exception ex)
        {
            ForegroundColor = ConsoleColor.Red;
            WriteLine($"Ошибка: {ex.Message}");
            ResetColor();
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
}