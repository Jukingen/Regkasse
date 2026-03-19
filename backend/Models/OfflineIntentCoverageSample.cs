using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Observability: one row per replayed offline intent to measure DeviceId/ClientSequenceNumber coverage.
    /// Used to compute deviceId missing rate, sequence missing rate, per-register coverage, and time-based trends.
    /// Does not change domain behaviour; replay never fails due to this recording.
    /// </summary>
    public class OfflineIntentCoverageSample
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [Column("created_at_utc")]
        public DateTime CreatedAtUtc { get; set; }

        [Required]
        public Guid CashRegisterId { get; set; }

        [Required]
        [Column("has_device_id")]
        public bool HasDeviceId { get; set; }

        [Required]
        [Column("has_client_sequence")]
        public bool HasClientSequence { get; set; }

        [Column("replay_batch_correlation_id")]
        public Guid? ReplayBatchCorrelationId { get; set; }
    }
}
