using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.DTOs;

public sealed class BackupRetentionPolicyResponseDto
{
    public int HotRetentionDays { get; init; }
    public int WarmRetentionDays { get; init; }
    public int ColdRetentionYears { get; init; }
    public bool ColdStorageEnabled { get; init; }
    public bool LegalRetentionEnforced { get; init; }
    public string CloudProvider { get; init; } = nameof(CloudStorageProviderKind.Filesystem);
    public bool CloudConfigured { get; init; }
    public bool CloudFallbackToFilesystem { get; init; }
    public string LegalBasis { get; init; } = "RKSV / BAO §132 (7-year retention)";
    public int SystemLegalRetentionDays { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string? UpdatedByUserId { get; init; }
    public BackupStorageCostResponseDto? StorageCosts { get; init; }
}

public sealed class BackupRetentionPolicyPutRequestDto
{
    public int HotRetentionDays { get; init; } = BackupRetentionWindows.DefaultHotDays;
    public int WarmRetentionDays { get; init; } = BackupRetentionWindows.DefaultWarmDays;
    public int ColdRetentionYears { get; init; } = BackupRetentionPolicySettings.DefaultColdRetentionYears;
    public bool ColdStorageEnabled { get; init; }
    public bool LegalRetentionEnforced { get; init; } = true;
}

public sealed class BackupRetentionStatusDto
{
    public Guid RunId { get; init; }
    public BackupStorageTier StorageTier { get; init; }
    public bool LegalHold { get; init; }
    public DateTime? LegalHoldUntilUtc { get; init; }
    public string? LegalHoldReason { get; init; }
    public DateTime? RetentionExpiresAtUtc { get; init; }
    public string Status { get; init; } = "hot";
    public bool CanDelete { get; init; }
    public bool InColdStorage { get; init; }
    public string? CloudLocator { get; init; }
}

public sealed class BackupLegalHoldRequestDto
{
    public bool LegalHold { get; init; } = true;
    public string? Reason { get; init; }
    public DateTime? UntilUtc { get; init; }
}

public sealed class BackupMoveToColdResponseDto
{
    public Guid RunId { get; init; }
    public bool Success { get; init; }
    public int ArtifactsArchived { get; init; }
    public int ArtifactsDeduplicated { get; init; }
    public string? Provider { get; init; }
    public string? Message { get; init; }
}
