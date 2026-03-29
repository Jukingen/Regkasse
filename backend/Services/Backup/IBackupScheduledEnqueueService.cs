using KasseAPI_Final.Data;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// UTC cron ile zamanlanmış yedek sıraya alma; dağıtık yedek advisory kilit tutulurken çağrılmalıdır.
/// </summary>
public interface IBackupScheduledEnqueueService
{
    /// <summary>Yeni bir zamanlanmış satır eklendiyse true.</summary>
    Task<bool> TryEnqueueIfDueAsync(AppDbContext db, CancellationToken cancellationToken = default);
}
