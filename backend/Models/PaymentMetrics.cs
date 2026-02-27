using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models
{
    // English Description: Performance metrics for payment operations
    public class PaymentMetrics : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public double ProcessingTimeMs { get; set; }

        [Required]
        public bool IsSuccess { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public int ServerLoad { get; set; }

        public long MemoryUsage { get; set; }

        [MaxLength(100)]
        public string? ErrorType { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        public int? RetryCount { get; set; }

        [MaxLength(50)]
        public string? PaymentGateway { get; set; }

        public bool? IsTseRequired { get; set; }

        public bool? IsTseAvailable { get; set; }

        public double? TseResponseTimeMs { get; set; }

        [MaxLength(100)]
        public string? NetworkLatency { get; set; }

        public int? DatabaseQueryCount { get; set; }

        public double? DatabaseQueryTimeMs { get; set; }
    }
}
