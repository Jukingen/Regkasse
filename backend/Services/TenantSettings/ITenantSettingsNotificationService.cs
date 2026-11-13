using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.TenantSettings;

/// <summary>
/// Activity feed + best-effort email for tenant settings four-eyes workflow.
/// Never throws into the settings change flow.
/// </summary>
public interface ITenantSettingsNotificationService
{
    Task NotifySettingsChangeAsync(
        Guid tenantId,
        Guid changeId,
        ActivityEventType eventType,
        string settingType,
        object? oldValue,
        object? newValue,
        string changedByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
