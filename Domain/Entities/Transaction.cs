using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities
{
    public class Transaction
    {
        [Key] public Guid Id { get; set; }

        public Guid WalletId { get; set; }
        public Wallet Wallet { get; set; } = null!;

        public DateTime Date { get; set; }

        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }

        public TransactionType Type { get; set; }

        [MaxLength(1000)] public string? Description { get; set; }
    }
}