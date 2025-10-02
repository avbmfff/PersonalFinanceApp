using Domain.Enums;

namespace Application.DTOs
{
    public record TransactionGroup(
        TransactionType Type,
        decimal TotalAmount,
        IReadOnlyList<TransactionDto> Transactions);
}