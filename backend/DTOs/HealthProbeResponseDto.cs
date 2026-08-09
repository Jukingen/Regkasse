using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

/// <summary>JSON body for <c>/api/health/ready</c> (and shared probe shape).</summary>
public sealed class HealthProbeResponseDto
{
    public string Status { get; init; } = string.Empty;

    public double TotalDurationMs { get; init; }

    public DateTime CheckedAtUtc { get; init; }

    /// <summary>
    /// Effective release stage (<c>dev</c>|<c>staging</c>|<c>canary</c>|<c>production</c>)
    /// from <c>Deployment:ReleaseStage</c> / <c>RELEASE_STAGE</c> (via <see cref="Services.Deployment.ReleaseStageResolver"/>).
    /// Exposed for all environments; primarily for debugging and Staging (Demo &amp; QA) verification / smoke checks.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReleaseStage { get; init; }

    /// <summary>
    /// Cache/Redis posture from the ready <c>cache</c> check: <c>Healthy</c> or <c>Degraded</c>.
    /// Omitted when the probe set does not include the cache check.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedisStatus { get; init; }

    public Dictionary<string, HealthProbeEntryDto> Entries { get; init; } =
        new(StringComparer.Ordinal);
}

/// <summary>Per-check entry inside a health probe response.</summary>
public sealed class HealthProbeEntryDto
{
    public string Status { get; init; } = string.Empty;

    public string? Description { get; init; }

    public double DurationMs { get; init; }

    public IReadOnlyDictionary<string, object> Data { get; init; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
}
