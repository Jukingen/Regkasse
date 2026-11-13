namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>license_reminders.status</c> values.</summary>
public static class LicenseReminderStatuses
{
    public const string Pending = "pending";
    public const string Sent = "sent";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Sent,
        Cancelled,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
