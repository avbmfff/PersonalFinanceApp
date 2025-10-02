namespace Application.DTOs
{
    public class TransactionDto
    {
        public Guid Id { get; init; }
        public Guid WalletId { get; init; }
        public DateTimeOffset Date { get; init; }
        public decimal Amount { get; init; }
        public string Type { get; init; } = string.Empty;
        public string? Description { get; init; }
    }
}