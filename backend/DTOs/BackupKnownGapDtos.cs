using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// Per-tenant acknowledgement stored at <c>tenant_settings</c> key <c>Backup:KnownGapsAccepted</c>.
/// This is not a compliance pass.
/// </summary>
public sealed class BackupKnownGapAcceptanceDto
{
    [JsonPropertyName("acceptedGaps")]
    public IReadOnlyList<string> AcceptedGaps { get; init; } = [];

    [JsonPropertyName("acceptedBy")]
    public string AcceptedBy { get; init; } = string.Empty;

    [JsonPropertyName("acceptedAtUtc")]
    public DateTimeOffset? AcceptedAtUtc { get; init; }
}

/// <summary>
/// One catalog gap plus the tenant acknowledgement and the current check.
/// <c>displayColor</c> is <c>green</c>, <c>amber</c>, or <c>red</c>.
/// Acceptance of an open gap yields amber. It does not yield green.
/// </summary>
public sealed class BackupKnownGapCardDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    [JsonPropertyName("open")]
    public bool Open { get; init; }

    [JsonPropertyName("accepted")]
    public bool Accepted { get; init; }

    [JsonPropertyName("acceptedAtUtc")]
    public DateTimeOffset? AcceptedAtUtc { get; init; }

    [JsonPropertyName("acceptedBy")]
    public string? AcceptedBy { get; init; }

    /// <summary><c>green</c> when the check is clear, <c>amber</c> when accepted, <c>red</c> when open and not accepted.</summary>
    [JsonPropertyName("displayColor")]
    public string DisplayColor { get; init; } = "green";
}

/// <summary>Catalog, stored acceptance, and the evaluated card for one mandant.</summary>
public sealed class BackupKnownGapViewDto
{
    [JsonPropertyName("catalogVersion")]
    public int CatalogVersion { get; init; }

    [JsonPropertyName("allowsGateOverride")]
    public bool AllowsGateOverride { get; init; }

    /// <summary><c>pass</c> or <c>fail</c>. Acceptance does not flip fail to pass unless <see cref="AllowsGateOverride"/> is true.</summary>
    [JsonPropertyName("verdict")]
    public string Verdict { get; init; } = "fail";

    [JsonPropertyName("acceptance")]
    public BackupKnownGapAcceptanceDto Acceptance { get; init; } = new();

    [JsonPropertyName("gaps")]
    public IReadOnlyList<BackupKnownGapCardDto> Gaps { get; init; } = [];
}
