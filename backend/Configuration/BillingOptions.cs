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

    /// <summary>When true, hosted service generates monthly SaaS invoices for active paid tenants.</summary>
    public bool AutoMonthlyInvoicingEnabled { get; set; } = true;

    /// <summary>Day of month (1–28) to run monthly invoice generation (UTC).</summary>
    public int MonthlyInvoiceDayOfMonth { get; set; } = 1;

    public int MonthlyInvoiceHourUtc { get; set; } = 6;

    public int MonthlyInvoiceMinuteUtc { get; set; } = 0;

    public decimal MonthlyNetStarter { get; set; } = 49m;

    public decimal MonthlyNetBusiness { get; set; } = 99m;

    public decimal MonthlyNetPlus { get; set; } = 149m;
}
