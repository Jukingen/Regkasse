using System;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Default for tests/services without hosted health probing (treats TSE as healthy).
/// </summary>
public sealed class AlwaysOnlineTseHealthMonitor : ITseHealthMonitor
{
    public static readonly AlwaysOnlineTseHealthMonitor Instance = new();

    private AlwaysOnlineTseHealthMonitor()
    {
    }

    public TseHealthSnapshot Snapshot { get; } = new()
    {
        Status = TseOperationalHealth.Online,
        LastCheckUtc = DateTime.UtcNow,
        LastSuccessfulPingUtc = DateTime.UtcNow,
        ConsecutiveFailures = 0
    };

    public event EventHandler<TseHealthChangedEventArgs>? StatusChanged
    {
        add { }
        remove { }
    }
}
