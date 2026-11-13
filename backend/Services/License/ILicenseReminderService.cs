namespace KasseAPI_Final.Services.License;

public sealed record LicenseReminderRunResult(
    int EmailsSent,
    int Skipped,
    int Failed);

/// <summary>
/// Scheduled mandant license expiry and grace-period email reminders
/// (calendar-day anchors around <see cref="Models.Tenant.LicenseValidUntilUtc"/>).
/// </summary>
public interface ILicenseReminderService
{
    /// <summary>
    /// Sends due expiry reminder emails for active tenants when <c>daysRemaining</c> matches a configured anchor
    /// (default 30 / 14 / 7 / 1) and optionally once when expired. Idempotent per tenant, expiry instant,
    /// and anchor via billing audit trail. Recipients: Mandanten-Admins (Manager), else owner, else tenant email.
    /// </summary>
    Task<LicenseReminderRunResult> SendDueMandantExpiryRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends grace-period reminder emails while a mandant is inside the configured grace window
    /// (default anchors 6 / 4 / 2 remaining days, plus urgent ≤1). Idempotent per tenant, expiry date,
    /// and remaining-grace day via billing audit trail.
    /// </summary>
    Task<LicenseReminderRunResult> SendDueGracePeriodRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends emails for billing <c>license_reminders</c> rows whose <c>reminder_date_utc</c> is due, then marks them sent.
    /// </summary>
    Task<int> SendDueBillingSaleRemindersAsync(CancellationToken cancellationToken = default);
}
