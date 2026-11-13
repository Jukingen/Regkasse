using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Activity;

public sealed class NotificationConfigService : INotificationConfigService
{
    private readonly AppDbContext _db;

    public NotificationConfigService(AppDbContext db) => _db = db;

    public async Task<NotificationConfig> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await _db.TenantNotificationConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var config = row?.Config ?? NotificationConfig.CreateDefault();
        config.DepExportMobilePush ??= DepExportMobilePushSettings.CreateDefault();
        return config;
    }

    public async Task<NotificationConfig> SaveAsync(
        Guid tenantId,
        NotificationConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var row = await _db.TenantNotificationConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (row == null)
        {
            config.DepExportMobilePush ??= DepExportMobilePushSettings.CreateDefault();
            row = new TenantNotificationConfig
            {
                TenantId = tenantId,
                Config = config,
                UpdatedAtUtc = now,
            };
            _db.TenantNotificationConfigs.Add(row);
        }
        else
        {
            // Preserve DEP mobile push prefs when callers omit them (legacy FA activity form).
            if (config.DepExportMobilePush is null && row.Config?.DepExportMobilePush is not null)
                config.DepExportMobilePush = row.Config.DepExportMobilePush;
            else
                config.DepExportMobilePush ??= DepExportMobilePushSettings.CreateDefault();

            row.Config = config;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Config;
    }
}
