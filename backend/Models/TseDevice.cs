using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    [Table("TseDevices")]
    public class TseDevice : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string DeviceType { get; set; } = string.Empty; // Epson-TSE, fiskaly

        [Required]
        [StringLength(100)]
        public string VendorId { get; set; } = string.Empty; // VID_04B8

        [Required]
        [StringLength(100)]
        public string ProductId { get; set; } = string.Empty; // PID_0E15

        [Required]
        public bool IsConnected { get; set; } = false;

        [Required]
        public DateTime LastConnectionTime { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime LastSignatureTime { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string CertificateStatus { get; set; } = "UNKNOWN"; // VALID, EXPIRED, REVOKED, UNKNOWN

        [Required]
        [StringLength(20)]
        public string MemoryStatus { get; set; } = "UNKNOWN"; // OK, LOW, FULL, UNKNOWN

        [Required]
        public bool CanCreateInvoices { get; set; } = false;

        [StringLength(500)]
        public string? ErrorMessage { get; set; }

        [Required]
        public int TimeoutSeconds { get; set; } = 30;

        // RKSV zorunlu alanlar
        [Required]
        public Guid KassenId { get; set; } = Guid.Empty;

        [Required]
        [StringLength(100)]
        public string FinanzOnlineUsername { get; set; } = string.Empty;

        [Required]
        public bool FinanzOnlineEnabled { get; set; } = false;

        [Required]
        public DateTime LastFinanzOnlineSync { get; set; } = DateTime.UtcNow;

        [Required]
        public int PendingInvoices { get; set; } = 0;

        [Required]
        public int PendingReports { get; set; } = 0;

        // --- Failover / multi-device registry (additive; legacy rows keep defaults) ---

        /// <summary>
        /// Owning tenant. Nullable because legacy <c>TseDevices</c> rows predate tenant scoping;
        /// not <see cref="ITenantEntity"/> until backfilled (query filters would hide old rows).
        /// </summary>
        public Guid? TenantId { get; set; }

        /// <summary>Optional FK to <see cref="CashRegister"/> (preferred over legacy <see cref="KassenId"/> for new links).</summary>
        public Guid? CashRegisterId { get; set; }

        /// <summary>Vendor / provider device identifier (e.g. fiskaly TSS id).</summary>
        [StringLength(200)]
        public string? DeviceId { get; set; }

        /// <summary>Provider label: fiskaly, epson, swissbit (complements <see cref="DeviceType"/>).</summary>
        [StringLength(64)]
        public string? Provider { get; set; }

        /// <summary>Encrypted API key ciphertext — never store plaintext.</summary>
        [StringLength(4000)]
        public string? ApiKey { get; set; }

        /// <summary>Encrypted API secret ciphertext — never store plaintext.</summary>
        [StringLength(4000)]
        public string? ApiSecret { get; set; }

        /// <summary>Optional certificate material / thumbprint metadata (not private key).</summary>
        [Column(TypeName = "text")]
        public string? Certificate { get; set; }

        public bool IsPrimary { get; set; } = true;

        /// <summary>When this row is a backup, the primary device it serves.</summary>
        public Guid? PrimaryDeviceId { get; set; }

        public bool IsBackup { get; set; }

        /// <summary>True while this backup is actively signing in place of its primary.</summary>
        public bool IsFailoverActive { get; set; }

        public TseHealthStatus HealthStatus { get; set; } = TseHealthStatus.Healthy;

        /// <summary>0–100 health score from probe / expiry policy.</summary>
        public int HealthScore { get; set; } = 100;

        public DateTime? LastHealthCheck { get; set; }

        [StringLength(1000)]
        public string? HealthMessage { get; set; }

        public DateTime? IssuedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? ExpiryWarningSentAt { get; set; }

        public DateTime? LastFailoverAt { get; set; }

        [StringLength(500)]
        public string? LastFailoverReason { get; set; }

        public int FailoverCount { get; set; }

        /// <summary>Operator-scheduled certificate renewal target (UTC). Null = not scheduled.</summary>
        public DateTime? ScheduledRenewalAt { get; set; }

        // Navigation
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant? Tenant { get; set; }

        [ForeignKey(nameof(CashRegisterId))]
        public virtual CashRegister? CashRegister { get; set; }

        [ForeignKey(nameof(PrimaryDeviceId))]
        public virtual TseDevice? PrimaryDevice { get; set; }

        public virtual ICollection<TseDevice> BackupDevices { get; set; } = new List<TseDevice>();
    }

    public enum TseDeviceType
    {
        EpsonTSE = 0,
        Fiskaly = 1,
        Custom = 2
    }

    public enum TseCertificateStatus
    {
        Valid = 0,
        Expired = 1,
        Revoked = 2,
        Unknown = 3
    }

    public enum TseMemoryStatus
    {
        Ok = 0,
        Low = 1,
        Full = 2,
        Unknown = 3
    }
}
