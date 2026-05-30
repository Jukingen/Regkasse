using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>Ensures a per-tenant <see cref="BackupScheduleConfiguration"/> row exists.</summary>
public static class BackupScheduleConfigurationEnsure
{
    public static async Task<BackupScheduleConfiguration> EnsureForTenantAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.BackupScheduleConfigurations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (existing != null)
            return existing;

        var utcNow = DateTime.UtcNow;
        var row = new BackupScheduleConfiguration
        {
            TenantId = tenantId,
            Enabled = false,
            ScheduleCron = BackupScheduleConfiguration.DefaultScheduleCron,
            RetentionDays = BackupScheduleConfiguration.DefaultRetentionDays,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsActive = true,
        };
        db.BackupScheduleConfigurations.Add(row);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return row;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.BackupScheduleConfigurations
                .FirstAsync(x => x.TenantId == tenantId, cancellationToken);
        }
    }

    public static BackupScheduleConfiguration CreateDefaultRow(Guid tenantId, DateTime utcNow) =>
        new()
        {
            TenantId = tenantId,
            Enabled = false,
            ScheduleCron = BackupScheduleConfiguration.DefaultScheduleCron,
            RetentionDays = BackupScheduleConfiguration.DefaultRetentionDays,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            IsActive = true,
        };
}
