using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Per-register, per-day sequence for fiscal receipt numbers (BelegNr).
    /// SEQ is numeric, monotonic, resets daily; gaps allowed; allocated numbers never reused.
    /// </summary>
    [Table("receipt_sequences")]
    public class ReceiptSequence
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        [Column("kassen_id")]
        public string KassenId { get; set; } = string.Empty;

        [Required]
        [Column("sequence_date", TypeName = "date")]
        public DateTime SequenceDate { get; set; }

        /// <summary>Next value to allocate for this (KassenId, date). After allocation this is incremented.</summary>
        [Required]
        [Column("next_sequence")]
        public int NextSequence { get; set; } = 1;

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
