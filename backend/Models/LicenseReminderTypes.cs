namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>license_reminders.reminder_type</c> values.</summary>
public static class LicenseReminderTypes
{
    public const string Expiry = "expiry";
    public const string Renewal = "renewal";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Expiry,
        Renewal,
    };

    public static bool IsValid(string? reminderType) =>
        !string.IsNullOrWhiteSpace(reminderType) && All.Contains(reminderType.Trim());
}
