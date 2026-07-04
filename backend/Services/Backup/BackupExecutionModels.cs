using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Context passed into execution adapters — no HTTP, no controller references.
/// </summary>
public sealed record BackupExecutionContext(
    Guid BackupRunId,
    string? CorrelationId,
    string AdapterKindLabel,
    CancellationToken CancellationToken,
    string TenantSlugForFileName = BackupRunTenantSlugResolver.DeploymentSlug,
    DateTime? ArtifactFileNameTimestampUtc = null);

/// <summary>
/// In-memory description of an artifact before persistence.
/// </summary>
public sealed class BackupArtifactDescriptor
{
    public BackupArtifactType ArtifactType { get; init; }
    public string StorageDescriptor { get; init; } = string.Empty;
    public long? ByteSize { get; init; }
    public string? ContentHashSha256 { get; init; }
    public string? MetadataJson { get; init; }

    /// <summary>
    /// When true and <see cref="BackupOptions.VerifyLogicalDumpFileOnDisk"/> is true, verifier recomputes SHA-256 from the file (artifact integrity, not restore proof).
    /// </summary>
    public bool RequireOnDiskHashVerification { get; init; }
}

/// <summary>
/// Result of a single execution attempt (physical/logical backup). Verification is a separate step.
/// </summary>
public sealed class BackupExecutionResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
    public IReadOnlyList<BackupArtifactDescriptor> Artifacts { get; init; } = Array.Empty<BackupArtifactDescriptor>();
}
