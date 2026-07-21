using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Per-register, per-day sequence for fiscal receipt numbers (BelegNr).
    /// Format: AT-{RegisterNumber}-{yyyyMMdd}-{counter}. One row per CashRegisterId per day.
    /// </summary>
    [Table("receipt_sequences")]
    public class ReceiptSequence
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }

        [Required]
        [Column("sequence_date", TypeName = "date")]
        public DateTime SequenceDate { get; set; }

        [Required]
        [Column("next_sequence")]
        public int NextSequence { get; set; } = 1;

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
