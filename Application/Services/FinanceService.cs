using System.Text.RegularExpressions;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class FinanceService : IFinanceService
    {
        private readonly FinanceDbContext _dbContext;
        private static readonly Regex IsoCurrencyRegex = new(@"^[A-Z]{3}$", RegexOptions.Compiled);

        public FinanceService(FinanceDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<WalletDto> CreateWalletAsync(string name, string currency, decimal initialBalance,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(currency) || !IsoCurrencyRegex.IsMatch(currency.ToUpperInvariant()))
                throw new ArgumentException("Currency must be ISO 3-letter code (e.g. USD).", nameof(currency));
            if (initialBalance < 0) throw new InvalidAmountException(initialBalance);

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Currency = currency.ToUpperInvariant(),
                InitialBalance = decimal.Round(initialBalance, 2)
            };

            _dbContext.Wallets.Add(wallet);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new WalletDto
            {
                Id = wallet.Id,
                Name = wallet.Name,
                Currency = wallet.Currency,
                InitialBalance = wallet.InitialBalance
            };
        }

        public async Task AddTransactionAsync(Guid walletId, DateTimeOffset date, decimal amount, TransactionType type,
            string? description = null, CancellationToken cancellationToken = default)
        {
            if (amount <= 0) throw new InvalidAmountException(amount);

            var wallet = await _dbContext.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);
            if (wallet == null) throw new WalletNotFoundException(walletId);

            var aggregates = await _dbContext.Transactions
                .Where(t => t.WalletId == walletId)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Incomes = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)t.Amount) ?? 0m
                })
                .FirstOrDefaultAsync(cancellationToken);

            var currentBalance = wallet.InitialBalance + (aggregates?.Incomes ?? 0) - (aggregates?.Expenses ?? 0);

            if (type == TransactionType.Expense && amount > currentBalance)
                throw new InsufficientFundsException(walletId, amount, currentBalance);

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                Date = date.Date,
                Amount = decimal.Round(amount, 2),
                Type = type,
                Description = description
            };

            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<decimal> GetCurrentBalanceAsync(Guid walletId, CancellationToken cancellationToken = default)
        {
            var wallet = await _dbContext.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);
            if (wallet == null) throw new WalletNotFoundException(walletId);

            var aggregates = await _dbContext.Transactions
                .Where(t => t.WalletId == walletId)
                .GroupBy(t => 1)
                .Select(g => new
                {
                    Incomes = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)t.Amount) ?? 0m
                })
                .FirstOrDefaultAsync(cancellationToken);

            return decimal.Round(wallet.InitialBalance + (aggregates?.Incomes ?? 0) - (aggregates?.Expenses ?? 0), 2);
        }

        public async Task<MonthlyTransactionsReport> GetMonthlyTransactionsGroupedByTypeAsync(int year, int month,
            CancellationToken cancellationToken = default)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0);
            var endDate = startDate.AddMonths(1);

            var transactions = await _dbContext.Transactions
                .AsNoTracking()
                .Where(t => t.Date >= startDate && t.Date < endDate)
                .OrderBy(t => t.Date)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    WalletId = t.WalletId,
                    Date = t.Date,
                    Amount = t.Amount,
                    Type = t.Type.ToString(),
                    Description = t.Description
                })
                .ToListAsync();


            var groups = transactions
                .GroupBy(t => Enum.Parse<TransactionType>(t.Type))
                .Select(g =>
                {
                    var total = g.Sum(t => t.Amount);
                    return new TransactionGroup(
                        g.Key,
                        decimal.Round(total, 2),
                        g.OrderBy(t => t.Date).ToList().AsReadOnly()
                    );
                })
                .OrderByDescending(g => g.TotalAmount)
                .ToList();

            return new MonthlyTransactionsReport
            {
                Year = year,
                Month = month,
                Groups = groups
            };
        }

        public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransactionDto>>> GetTopExpensesPerWalletAsync(
            int year, int month, int topN = 3, CancellationToken cancellationToken = default)
        {
            var startDate = new DateTime(year, month, 1, 0, 0, 0);
            var endDate = startDate.AddMonths(1);

            var allExpenses = await _dbContext.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense && t.Date >= startDate && t.Date < endDate)
                .ToListAsync();

            var perWallet = allExpenses
                .GroupBy(t => t.WalletId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<TransactionDto>)g
                        .OrderByDescending(t => t.Amount)
                        .Take(topN)
                        .Select(t => new TransactionDto
                        {
                            Id = t.Id,
                            WalletId = t.WalletId,
                            Date = t.Date,
                            Amount = t.Amount,
                            Type = t.Type.ToString(),
                            Description = t.Description
                        })
                        .ToList()
                );

            return perWallet;
        }
    }
}