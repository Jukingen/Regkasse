namespace KasseAPI_Final.Services.Billing;

public interface IBillingReminderService
{
    /// <summary>Create expiry reminders (30/15/7 days) for an active license sale.</summary>
    Task ScheduleRemindersForSaleAsync(Guid saleId, CancellationToken ct = default);

    /// <summary>Cancel pending reminders when a sale is cancelled or superseded.</summary>
    Task CancelRemindersForSaleAsync(Guid saleId, CancellationToken ct = default);

    /// <summary>Process due pending reminders; returns count marked as sent.</summary>
    Task<int> ProcessDueRemindersAsync(CancellationToken ct = default);

    Task<BillingReminderListResponse> ListAsync(
        BillingReminderQuery query,
        CancellationToken ct = default);
}
