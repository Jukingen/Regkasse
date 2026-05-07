using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Post-commit hook after a backup run reaches <see cref="BackupRunStatus.Succeeded"/>.</summary>
public interface IBackupPostSuccessOrchestrationHook
{
    Task NotifySucceededAsync(AppDbContext db, BackupRun run, CancellationToken cancellationToken = default);
}
