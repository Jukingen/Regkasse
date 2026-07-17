using System.Text.Json.Serialization;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Aggregated backup restore-readiness / RKSV product-gate status (30-day window).
/// Not a BMF/RKSV legal certification — metadata gates for validation restore.
/// </summary>
public sealed class BackupComplianceStatusResponseDto
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("compliant")]
    public int Compliant { get; init; }

    [JsonPropertyName("nonCompliant")]
    public int NonCompliant { get; init; }

    [JsonPropertyName("allCompliant")]
    public bool AllCompliant { get; init; }

    [JsonPropertyName("lastCheckUtc")]
    public DateTime LastCheckUtc { get; init; }

    [JsonPropertyName("disclaimer")]
    public string Disclaimer { get; init; } =
        "Product restore-readiness gates (hash, strategy, Succeeded). Not official RKSV/BMF certification.";

    [JsonPropertyName("restoreRequestsTotal")]
    public int RestoreRequestsTotal { get; init; }

    [JsonPropertyName("restoreRequestsCompleted")]
    public int RestoreRequestsCompleted { get; init; }

    [JsonPropertyName("restoreRequestsFailed")]
    public int RestoreRequestsFailed { get; init; }

    [JsonPropertyName("backups")]
    public List<BackupComplianceListItemDto> Backups { get; init; } = new();
}

public sealed class BackupComplianceListItemDto
{
    [JsonPropertyName("backupRunId")]
    public Guid BackupRunId { get; init; }

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }

    [JsonPropertyName("tenantId")]
    public Guid? TenantId { get; init; }

    [JsonPropertyName("tenantName")]
    public string? TenantName { get; init; }

    [JsonPropertyName("strategy")]
    public BackupStrategyKind Strategy { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("compliant")]
    public bool Compliant { get; init; }

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}
