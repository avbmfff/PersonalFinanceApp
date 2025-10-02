using Domain.Entities;
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
    }
}