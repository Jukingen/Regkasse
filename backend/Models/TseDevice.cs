using System;
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
