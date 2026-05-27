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

        return row?.Config ?? NotificationConfig.CreateDefault();
    }

    public async Task<NotificationConfig> SaveAsync(
        Guid tenantId,
        NotificationConfig config,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.TenantNotificationConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        if (row == null)
        {
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
            row.Config = config;
            row.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return row.Config;
    }
}
