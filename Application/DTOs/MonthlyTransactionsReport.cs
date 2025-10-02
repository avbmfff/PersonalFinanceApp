namespace Application.DTOs
{
    public class MonthlyTransactionsReport
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public IReadOnlyList<TransactionGroup> Groups { get; init; } = Array.Empty<TransactionGroup>();
    }
}