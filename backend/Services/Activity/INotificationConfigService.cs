using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

public interface INotificationConfigService
{
    Task<NotificationConfig> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<NotificationConfig> SaveAsync(
        Guid tenantId,
        NotificationConfig config,
        CancellationToken cancellationToken = default);
}
