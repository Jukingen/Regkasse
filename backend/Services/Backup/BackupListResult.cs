using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Paged backup run list for <see cref="IBackupService.ListBackupsAsync"/>.</summary>
public sealed class BackupListResult
{
    public required IReadOnlyList<BackupListItem> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class BackupListItem
{
    public Guid BackupRunId { get; init; }
    public BackupStrategyKind Strategy { get; init; }
    public Guid? TenantId { get; init; }
    public BackupRunStatus Status { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public long? LogicalDumpBytes { get; init; }
}
