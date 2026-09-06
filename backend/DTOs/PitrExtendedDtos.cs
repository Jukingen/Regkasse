namespace KasseAPI_Final.DTOs;

public sealed class BackupChainItemDto
{
    public Guid RunId { get; init; }

    /// <summary><c>full</c>, <c>incremental</c>, or <c>system</c>.</summary>
    public string PackageKind { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? TenantSlug { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public DateTime? IncrementalSinceUtc { get; init; }

    public Guid? ParentRunId { get; init; }

    public bool CoveredByWal { get; init; }
}

public sealed class BackupChainResponseDto
{
    public Guid? TenantIdFilter { get; init; }

    public BackupChainItemDto? FullBackup { get; init; }

    public IReadOnlyList<BackupChainItemDto> Incrementals { get; init; } = Array.Empty<BackupChainItemDto>();

    public IReadOnlyList<BackupChainItemDto> SystemBackups { get; init; } = Array.Empty<BackupChainItemDto>();

    public DateTime? WalCoverageStartUtc { get; init; }

    public DateTime? WalCoverageEndUtc { get; init; }

    public int WalFileCount { get; init; }

    public bool RestorePointAvailable { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class WalArchiveStatusDto
{
    public bool Enabled { get; init; }

    public bool DirectoryExists { get; init; }

    public string? Directory { get; init; }

    public int FileCount { get; init; }

    public DateTime? OldestFileUtc { get; init; }

    public DateTime? NewestFileUtc { get; init; }

    public int RetentionDays { get; init; }

    public int SwitchIntervalMinutes { get; init; }

    public int? LagMinutes { get; init; }

    public bool HostArchiveCommandRequired { get; init; } = true;

    public string Message { get; init; } = string.Empty;
}

public sealed class PitrCheckResultDto
{
    public string Name { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Detail { get; init; }
}

public sealed class PitrPreRestoreValidationDto
{
    public bool Passed { get; init; }

    public DateTime? TargetTimeUtc { get; init; }

    public Guid? BaseBackupId { get; init; }

    public string RecoveryMethod { get; init; } = string.Empty;

    public int EstimatedDataLossSeconds { get; init; }

    public PitrCheckResultDto Hash { get; init; } = new() { Name = "hash" };

    public PitrCheckResultDto Schema { get; init; } = new() { Name = "schema" };

    public PitrCheckResultDto TseChain { get; init; } = new() { Name = "tse_chain" };

    public BackupChainResponseDto? Chain { get; init; }

    public RestorePointValidationResultDto? RestorePoint { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class PitrDryRunRequestDto
{
    public DateTime TargetTimeUtc { get; init; }

    public Guid? TenantId { get; init; }
}

public sealed class PitrDryRunResponseDto
{
    public bool Accepted { get; init; }

    public Guid? DrillRunId { get; init; }

    public Guid? BaseBackupId { get; init; }

    public DateTime? TargetTimeUtc { get; init; }

    public PitrPreRestoreValidationDto? Validation { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class IncrementalBackupTriggerRequestDto
{
    public Guid? TenantId { get; init; }

    public DateTime? SinceUtc { get; init; }
}

public sealed class IncrementalRestorePlanDto
{
    public Guid TenantId { get; init; }

    public Guid? FullBackupId { get; init; }

    public DateTime? FullBackupCompletedAtUtc { get; init; }

    public IReadOnlyList<Guid> IncrementalBackupIds { get; init; } = Array.Empty<Guid>();

    public Guid? NearestSystemDumpId { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed class IncrementalRestoreResultDto
{
    public IncrementalRestorePlanDto Plan { get; init; } = new();

    public bool IsolatedDryRunEnqueued { get; init; }

    public Guid? DrillRunId { get; init; }

    public string Message { get; init; } = string.Empty;
}
