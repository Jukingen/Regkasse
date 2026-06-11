using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Per-register TSE signature chain state. One row per CashRegisterId.
    /// </summary>
    [Table("signature_chain_state")]
    public class SignatureChainState : ITenantEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [Required]
        [Column("cash_register_id")]
        public Guid CashRegisterId { get; set; }

        [Column("last_signature", TypeName = "text")]
        public string? LastSignature { get; set; }

        [Required]
        [Column("last_counter")]
        public int LastCounter { get; set; }

        /// <summary>Cumulative gross turnover in Euro cents (RKSV Umsatzzähler plaintext).</summary>
        [Required]
        [Column("last_turnover_counter_cents")]
        public long LastTurnoverCounterCents { get; set; }

        [Required]
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
