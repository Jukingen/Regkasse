using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Per-register TSE signature chain state. One row per CashRegisterId.
    /// </summary>
    [Table("signature_chain_state")]
    public class SignatureChainState
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }

        [MaxLength(4000)]
        [Column("last_signature")]
        public string? LastSignature { get; set; }

        [Required]
        [Column("last_counter")]
        public int LastCounter { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
