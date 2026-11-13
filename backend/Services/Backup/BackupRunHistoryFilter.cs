using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Optional list filters for <see cref="IBackupRunQueryService.GetHistoryAsync"/>.</summary>
public sealed class BackupRunHistoryFilter
{
    public BackupStrategyKind? Strategy { get; init; }

    /// <summary>
    /// <c>system</c> / <c>cron</c> → scheduled runs; otherwise treated as <c>requested_by_user_id</c>.
    /// </summary>
    public string? CreatedBy { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}
