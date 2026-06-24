namespace KasseAPI_Final.Services.Billing;

public interface IReminderService
{
    /// <summary>Scan active license sales and create pending expiry reminders.</summary>
    Task CheckAndCreateRemindersAsync(CancellationToken ct = default);

    /// <summary>Send due pending reminders (email, activity feed).</summary>
    Task SendPendingRemindersAsync(CancellationToken ct = default);

    Task<List<LicenseReminderResponse>> GetForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task MarkAsSentAsync(Guid reminderId, CancellationToken ct = default);
}
