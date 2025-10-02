namespace Application.DTOs
{
    public class WalletDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Currency { get; init; } = string.Empty;
        public decimal InitialBalance { get; init; }
    }
}