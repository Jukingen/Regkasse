using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Registrierkasse_API.Models
{
    public class SystemConfiguration : BaseEntity
    {
        [Required]
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Description { get; set; }
        // OperationMode, OfflineSettings, TseSettings, PrinterSettings, Settings kaldırıldı
    }
} 
