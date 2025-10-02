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
            if (!CurrencyValidator.IsValid(currency))
                throw new ArgumentException($"Unsupported currency code: {currency}", nameof(currency));

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Currency = currency.ToUpperInvariant(),
                InitialBalance = decimal.Round(initialBalance, 2)
            };

            try
            {
                _dbContext.Wallets.Add(wallet);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new DataAccessException("Failed to create wallet.", ex);
            }

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

            var wallet = await _dbContext.Wallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);
            if (wallet == null) throw new WalletNotFoundException(walletId);

            // aggregate in DB
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
                Date = date,
                Amount = decimal.Round(amount, 2),
                Type = type,
                Description = description
            };

            try
            {
                _dbContext.Transactions.Add(transaction);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new DataAccessException("Failed to add transaction.", ex);
            }
        }

        public async Task<decimal> GetCurrentBalanceAsync(Guid walletId, CancellationToken cancellationToken = default)
        {
            var wallet = await _dbContext.Wallets
                .AsNoTracking()
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
            return decimal.Round(currentBalance, 2);
        }

        public async Task<MonthlyTransactionsReport> GetMonthlyTransactionsGroupedByTypeAsync(int year, int month,
            CancellationToken cancellationToken = default)
        {
            var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            // Для SQLite сначала загружаем все транзакции в память
            var allTransactions = await _dbContext.Transactions
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var transactions = allTransactions
                .Where(t => t.Date.UtcDateTime >= start && t.Date.UtcDateTime < end)
                .OrderBy(t => t.Date.UtcDateTime)
                .ToList();

            var groups = transactions
                .GroupBy(t => t.Type)
                .Select(g =>
                {
                    var total = g.Sum(t => t.Amount);
                    var ordered = g.OrderBy(t => t.Date.UtcDateTime)
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
                        .AsReadOnly();

                    return new TransactionGroup(g.Key, decimal.Round(total, 2), ordered);
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
            int year,
            int month, int topN = 3, CancellationToken cancellationToken = default)
        {
            var start = new DateTimeOffset(new DateTime(year, month, 1), TimeSpan.Zero);
            var end = start.AddMonths(1);

            var allTransactions = await _dbContext.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Expense)
                .ToListAsync(cancellationToken);

            var transactions = allTransactions
                .Where(t => t.Date >= start && t.Date < end)
                .ToList();

            var perWallet = transactions
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