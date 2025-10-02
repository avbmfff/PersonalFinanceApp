using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities
{
    public class Wallet
    {
        [Key] public Guid Id { get; set; }

        [Required] [MaxLength(200)] public string Name { get; set; } = string.Empty;

        // ISO currency code, e.g. "USD", "EUR"
        [Required] [MaxLength(3)] public string Currency { get; set; } = "USD";

        [Column(TypeName = "decimal(18,2)")] public decimal InitialBalance { get; set; }

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

        [Timestamp] public byte[]? RowVersion { get; set; }
    }
}