namespace KasseAPI_Final.Services;

/// <summary>Thread-safe projection of recent license reminders for POS + admin polling.</summary>
public interface ILicenseReminderNotificationStore
{
    /// <summary>Replaces entire active reminder set (latest daily evaluation).</summary>
    void SetReminders(IReadOnlyList<LicenseReminderNotice>? items);

    IReadOnlyList<LicenseReminderNotice> GetReminders();
}

public sealed class LicenseReminderNotificationStore : ILicenseReminderNotificationStore
{
    private readonly object _gate = new();
    private IReadOnlyList<LicenseReminderNotice> _items = [];

    public void SetReminders(IReadOnlyList<LicenseReminderNotice>? items)
    {
        var next = items is { Count: > 0 } ? items : [];
        lock (_gate)
        {
            _items = next;
        }
    }

    public IReadOnlyList<LicenseReminderNotice> GetReminders()
    {
        lock (_gate)
        {
            return _items;
        }
    }
}
