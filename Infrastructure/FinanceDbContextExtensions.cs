using Domain.Entities;
using Domain.Enums;

namespace Infrastructure
{
    public static class FinanceDbContextExtensions
    {
        private static readonly Random Rnd = new();

        public static void SeedTestData(this FinanceDbContext db)
        {
            if (db.Wallets.Any())
                return;

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
                    var type = Rnd.Next(0, 2) == 0 ? TransactionType.Income : TransactionType.Expense;
                    var amount = Math.Round(Rnd.NextDouble() * 500 + 10, 2); // от 10 до 510
                    var daysAgo = Rnd.Next(0, 90);
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
    }
}