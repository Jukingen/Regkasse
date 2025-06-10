using System.ComponentModel.DataAnnotations;

namespace Registrierkasse_API.Models
{
    public class SystemConfiguration : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        public string OperationMode { get; set; } = "online-only"; // online-only, offline-only, hybrid
        
        public OfflineSettings OfflineSettings { get; set; } = new();
        public TseSettings TseSettings { get; set; } = new();
        public PrinterSettings PrinterSettings { get; set; } = new();
    }

    public class OfflineSettings
    {
        public bool Enabled { get; set; } = false;
        public int SyncInterval { get; set; } = 5; // dakika
        public int MaxOfflineDays { get; set; } = 7;
        public bool AutoSync { get; set; } = false;
    }

    public class TseSettings
    {
        public bool Required { get; set; } = true;
        public bool OfflineAllowed { get; set; } = false;
        public int MaxOfflineTransactions { get; set; } = 100;
    }

    public class PrinterSettings
    {
        public bool Required { get; set; } = true;
        public bool OfflineQueue { get; set; } = false;
        public int MaxQueueSize { get; set; } = 50;
    }
} 