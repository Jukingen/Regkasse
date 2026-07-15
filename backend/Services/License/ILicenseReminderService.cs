namespace KasseAPI_Final.Services.License;

public sealed record LicenseReminderRunResult(
    int EmailsSent,
    int Skipped,
    int Failed);

/// <summary>
/// Scheduled mandant license expiry email reminders (calendar-day anchors before <see cref="Models.Tenant.LicenseValidUntilUtc"/>).
/// </summary>
public interface ILicenseReminderService
{
    /// <summary>
    /// Sends due expiry reminder emails for active tenants when <c>daysRemaining</c> matches a configured anchor
    /// (default 30 / 15 / 7 / 3 / 1). Idempotent per tenant, expiry instant, and anchor via billing audit trail.
    /// </summary>
    Task<LicenseReminderRunResult> SendDueMandantExpiryRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends emails for billing <c>license_reminders</c> rows whose <c>reminder_date_utc</c> is due, then marks them sent.
    /// </summary>
    Task<int> SendDueBillingSaleRemindersAsync(CancellationToken cancellationToken = default);
}
