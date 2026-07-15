namespace KasseAPI_Final.Configuration;

/// <summary>Super Admin Mandanten billing (license sales, reminders).</summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>Calendar-day anchors before <c>license_sales.valid_until_utc</c> for scheduled reminders.</summary>
    public int[] ReminderDaysBeforeExpiry { get; set; } = [30, 15, 7, 3, 1];

    /// <summary>UTC hour (0–23) for the daily billing reminder tick.</summary>
    public int ReminderCheckHourUtc { get; set; } = 9;

    /// <summary>UTC minute (0–59) for the daily billing reminder tick.</summary>
    public int ReminderCheckMinuteUtc { get; set; } = 0;
}
