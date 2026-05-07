using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Ensures the singleton backup_settings row exists (migration seeds production; tests may use empty databases).</summary>
public static class BackupSettingsEnsure
{
    public static async Task EnsureSingletonAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.BackupSettings.AsNoTracking()
                .AnyAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken))
            return;

        db.BackupSettings.Add(new BackupSettings
        {
            Id = BackupSettings.SingletonId,
            Enabled = false,
            ScheduleCron = BackupSettings.DefaultScheduleCron,
            RetentionDays = BackupSettings.DefaultRetentionDays,
            LastRunAt = null,
            NextRunAt = null,
            UpdatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.BackupSettings.AsNoTracking()
                    .AnyAsync(x => x.Id == BackupSettings.SingletonId, cancellationToken))
                throw;
        }
    }
}
