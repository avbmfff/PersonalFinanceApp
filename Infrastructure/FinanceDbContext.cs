using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class FinanceDbContext : DbContext
    {
        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<Transaction> Transactions => Set<Transaction>();

        public FinanceDbContext(DbContextOptions<FinanceDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Wallet>(builder =>
            {
                builder.HasKey(w => w.Id);
                builder.Property(w => w.Name).IsRequired().HasMaxLength(200);
                builder.Property(w => w.Currency).IsRequired().HasMaxLength(3);
                builder.Property(w => w.InitialBalance).HasColumnType("decimal(18,2)");
                builder.Property(w => w.RowVersion).IsRowVersion();
                builder.HasMany(w => w.Transactions)
                    .WithOne(t => t.Wallet)
                    .HasForeignKey(t => t.WalletId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Transaction>(builder =>
            {
                builder.HasKey(t => t.Id);
                builder.Property(t => t.Amount).HasColumnType("decimal(18,2)");
                builder.Property(t => t.Description).HasMaxLength(1000);
                builder.Property(t => t.Type).HasConversion<int>();
            });
        }

        public static void Seed(FinanceDbContext db)
        {
            if (!db.Wallets.Any())
            {
                var wallet1 = new Wallet
                {
                    Id = Guid.NewGuid(),
                    Name = "Основной",
                    Currency = "USD",
                    InitialBalance = 500m
                };
                var wallet2 = new Wallet
                {
                    Id = Guid.NewGuid(),
                    Name = "Карта",
                    Currency = "EUR",
                    InitialBalance = 1000m
                };
                db.Wallets.AddRange(wallet1, wallet2);
                db.SaveChanges();

                db.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = wallet1.Id,
                    Date = DateTimeOffset.UtcNow.AddDays(-2),
                    Amount = 50m,
                    Type = TransactionType.Expense,
                    Description = "Продукты"
                });
                db.SaveChanges();
            }
        }
    }
}