namespace KasseAPI_Final.DTOs;

public sealed class PitrAvailabilityResponseDto
{
    public bool IsAvailable { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>Instance-wide unless <see cref="TenantIdFilter"/> was applied (manual idempotency key hint only).</summary>
    public Guid? TenantIdFilter { get; init; }

    public DateTime? EarliestRestorePointUtc { get; init; }

    public DateTime? LatestRestorePointUtc { get; init; }

    public IReadOnlyList<DateTime> SupportedTimePointsUtc { get; init; } = Array.Empty<DateTime>();

    public bool WalArchivingEnabled { get; init; }

    public int? WalArchiveLagMinutes { get; init; }
}

public sealed class ValidatePitrRestorePointRequestDto
{
    public DateTime TargetTimeUtc { get; init; }
}

public sealed class RestorePointValidationResultDto
{
    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;

    public Guid? TenantIdFilter { get; init; }

    public Guid? BaseBackupId { get; init; }

    public DateTime? BaseBackupTimeUtc { get; init; }

    public DateTime? TargetTimeUtc { get; init; }

    public DateTime? WalCoverageStartUtc { get; init; }

    public DateTime? WalCoverageEndUtc { get; init; }

    public int EstimatedDataLossSeconds { get; init; }

    /// <summary><c>PITR</c> or <c>FullBackupOnly</c>.</summary>
    public string RecoveryMethod { get; init; } = string.Empty;
}
