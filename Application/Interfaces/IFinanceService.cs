using Application.DTOs;
using Domain.Enums;

namespace Application.Interfaces
{
    public interface IFinanceService
    {
        Task<WalletDto> CreateWalletAsync(string name, string currency, decimal initialBalance,
            CancellationToken cancellationToken = default);

        Task AddTransactionAsync(Guid walletId, DateTimeOffset date, decimal amount, TransactionType type,
            string? description = null, CancellationToken cancellationToken = default);

        Task<decimal> GetCurrentBalanceAsync(Guid walletId, CancellationToken cancellationToken = default);

        Task<MonthlyTransactionsReport> GetMonthlyTransactionsGroupedByTypeAsync(int year, int month,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransactionDto>>> GetTopExpensesPerWalletAsync(int year, int month,
            int topN = 3, CancellationToken cancellationToken = default);
    }
}