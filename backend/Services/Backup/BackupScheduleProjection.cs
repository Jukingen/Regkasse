using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Scheduled backup history helpers for cron anchoring / status.</summary>
public static class BackupScheduleProjection
{
    /// <summary>Latest scheduled run request time (any status), or null.</summary>
    public static async Task<DateTime?> GetLastScheduledRequestedAtAsync(AppDbContext db, CancellationToken cancellationToken)
        => await db.BackupRuns.AsNoTracking()
            .Where(r => r.TriggerSource == BackupTriggerSource.Scheduled)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => (DateTime?)r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
