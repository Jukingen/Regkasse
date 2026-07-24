using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Severity for TSE statistical anomaly findings (diagnostic, not fiscal).</summary>
public static class TseAnomalySeverities
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
    public const string Info = "Info";

    public static readonly string[] All = { Critical, High, Medium, Low, Info };

    public static int Rank(string? severity) => severity switch
    {
        Critical => 5,
        High => 4,
        Medium => 3,
        Low => 2,
        Info => 1,
        _ => 0,
    };

    public static string Max(string a, string b) =>
        Rank(a) >= Rank(b) ? (a ?? Info) : (b ?? Info);

    public static string FromDeviationPercent(double absDeviationPercent) =>
        absDeviationPercent >= 80 ? Critical
        : absDeviationPercent >= 50 ? High
        : absDeviationPercent >= 30 ? Medium
        : absDeviationPercent >= 15 ? Low
        : Info;
}

/// <summary>Known metric keys for TSE anomaly detection.</summary>
public static class TseAnomalyMetrics
{
    public const string HealthScore = "HealthScore";
    public const string ResponseTimeMs = "ResponseTimeMs";
    public const string ErrorRatePercent = "ErrorRatePercent";
    public const string DailyTransactionVolume = "DailyTransactionVolume";
}

/// <summary>
/// Persisted TSE anomaly finding from statistical baseline deviation
/// (mean / MAD style — not a certified ML model).
/// </summary>
[Table("tse_anomalies")]
public sealed class TseAnomaly
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("device_id")]
    public Guid? DeviceId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("metric_name")]
    public string MetricName { get; set; } = string.Empty;

    [Column("current_value")]
    public double CurrentValue { get; set; }

    [Column("expected_value")]
    public double ExpectedValue { get; set; }

    /// <summary>Absolute percentage deviation from expected.</summary>
    [Column("deviation_percent")]
    public double DeviationPercent { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("severity")]
    public string Severity { get; set; } = TseAnomalySeverities.Info;

    [Required]
    [MaxLength(1000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("suggested_action")]
    public string? SuggestedAction { get; set; }

    [Column("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [Column("is_resolved")]
    public bool IsResolved { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(450)]
    [Column("resolved_by")]
    public string? ResolvedBy { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public TseDevice? Device { get; set; }
}
