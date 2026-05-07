using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("cash_registers")]
    public class CashRegister : BaseEntity
    {
        /// <summary>FK to <see cref="Tenant"/>; register numbers are unique per tenant.</summary>
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(20)]
        public string RegisterNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal StartingBalance { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; }

        [Required]
        public DateTime LastBalanceUpdate { get; set; }

        [Required]
        public RegisterStatus Status { get; set; }

        /// <summary>Operational shift owner (who opened this register). Not the same as per-user preference in UserSettings.</summary>
        public string? CurrentUserId { get; set; }

        /// <summary>UTC instant when the last RKSV Monatsbeleg (or December Jahresbeleg closing the month) was issued for this register.</summary>
        [Column("last_monatsbeleg_utc", TypeName = "timestamptz")]
        public DateTime? LastMonatsbelegUtc { get; set; }

        /// <summary>UTC instant when the last RKSV Jahresbeleg was issued for this register.</summary>
        [Column("last_jahresbeleg_utc", TypeName = "timestamptz")]
        public DateTime? LastJahresbelegUtc { get; set; }

        /// <summary>UTC instant when the RKSV Startbeleg was issued for this register (one-time commissioning).</summary>
        [Column("startbeleg_created_at", TypeName = "timestamptz")]
        public DateTime? StartbelegCreatedAt { get; set; }

        /// <summary>Last known server vs NTP offset (seconds) copied from global sync — dashboard drift visibility per register.</summary>
        [Column("last_server_time_offset_seconds")]
        public double? LastServerTimeOffsetSeconds { get; set; }

        /// <summary>UTC when <see cref="LastServerTimeOffsetSeconds"/> was last updated.</summary>
        [Column("last_server_time_drift_at_utc", TypeName = "timestamptz")]
        public DateTime? LastServerTimeDriftAtUtc { get; set; }

        // Navigation properties
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        public virtual ApplicationUser? CurrentUser { get; set; }
        public virtual ICollection<CashRegisterTransaction> Transactions { get; set; } = new List<CashRegisterTransaction>();
    }

    public enum RegisterStatus
    {
        Closed = 1,
        Open = 2,
        Maintenance = 3,
        Disabled = 4,

        /// <summary>RKSV Schlussbeleg issued; register must not open shifts or accept new fiscal receipts (permanent decommissioning).</summary>
        Decommissioned = 5,
    }
}
