using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Catalog update kinds for Super Admin TSE zero-downtime rolling updates.</summary>
public static class TseUpdateTypes
{
    public const string HealthProbePolicy = "HealthProbePolicy";
    public const string FailoverPolicy = "FailoverPolicy";
    public const string CostCatalog = "CostCatalog";
    public const string ProviderManifest = "ProviderManifest";
    public const string CertificatePolicy = "CertificatePolicy";

    public static readonly string[] All =
    {
        HealthProbePolicy,
        FailoverPolicy,
        CostCatalog,
        ProviderManifest,
        CertificatePolicy,
    };
}

public static class TseUpdateRiskLevels
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}

public static class TseUpdateRunStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Blocked = "Blocked";
}

/// <summary>Current applied catalog version per tenant and update type.</summary>
[Table("tse_update_states")]
public sealed class TseUpdateState
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("update_type")]
    public string UpdateType { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("current_version")]
    public string CurrentVersion { get; set; } = "0.0.0";

    [Column("last_checked_at")]
    public DateTime? LastCheckedAt { get; set; }

    [Column("last_applied_at")]
    public DateTime? LastAppliedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}

/// <summary>
/// History of TSE catalog / policy updates applied with zero-downtime rolling strategy.
/// Does not mutate fiscal signing keys or receipt chains.
/// </summary>
[Table("tse_update_history")]
public sealed class TseUpdateHistoryEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("update_type")]
    public string UpdateType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("risk_level")]
    public string RiskLevel { get; set; } = TseUpdateRiskLevels.Low;

    [Required]
    [MaxLength(64)]
    [Column("from_version")]
    public string FromVersion { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("to_version")]
    public string ToVersion { get; set; } = string.Empty;

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseUpdateRunStatuses.Succeeded;

    [Column("zero_downtime")]
    public bool ZeroDowntime { get; set; } = true;

    [Column("devices_touched")]
    public int DevicesTouched { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(450)]
    [Column("applied_by")]
    public string? AppliedBy { get; set; }

    [MaxLength(2000)]
    [Column("message")]
    public string? Message { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}
