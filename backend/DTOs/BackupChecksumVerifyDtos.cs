namespace KasseAPI_Final.DTOs;

/// <summary>On-demand or scheduled SHA-256 re-hash result for a backup run.</summary>
public sealed class BackupChecksumVerifyResponseDto
{
    public Guid RunId { get; init; }

    public bool IsValid { get; init; }

    public DateTime VerifiedAtUtc { get; init; }

    public string VerifierSource { get; init; } = string.Empty;

    public Guid? VerificationId { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<BackupChecksumArtifactResultDto> Artifacts { get; init; } =
        Array.Empty<BackupChecksumArtifactResultDto>();
}

public sealed class BackupChecksumArtifactResultDto
{
    public string ArtifactType { get; init; } = string.Empty;

    public string? StoredChecksum { get; init; }

    public string? ComputedChecksum { get; init; }

    /// <summary>passed | failed | missing_hash | missing_file</summary>
    public string Status { get; init; } = string.Empty;

    public string? Detail { get; init; }
}
