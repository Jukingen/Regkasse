namespace KasseAPI_Final.DTOs;

/// <summary>
/// Manual on-demand verify: SHA-256 artifact re-hash (same as automatic / scheduled)
/// plus optional TOC/table row counts from the verification report.
/// </summary>
public sealed class BackupManualVerifyResponseDto
{
    public Guid BackupId { get; init; }

    public bool IsValid { get; init; }

    public DateTime VerifiedAtUtc { get; init; }

    public string VerifierSource { get; init; } = string.Empty;

    public Guid? VerificationId { get; init; }

    public string? FailureReason { get; init; }

    public IReadOnlyList<BackupChecksumArtifactResultDto> Artifacts { get; init; } =
        Array.Empty<BackupChecksumArtifactResultDto>();

    /// <summary>TOC vs live table statistics; null when the dump cannot be analyzed.</summary>
    public BackupVerificationReportDto? TableReport { get; init; }
}
