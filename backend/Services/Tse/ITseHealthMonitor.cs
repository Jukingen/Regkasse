using System;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Read-only view of in-memory TSE health (updated by background probing).
/// </summary>
public interface ITseHealthMonitor
{
    TseHealthSnapshot Snapshot { get; }

    event EventHandler<TseHealthChangedEventArgs>? StatusChanged;
}

public sealed class TseHealthChangedEventArgs : EventArgs
{
    public TseHealthSnapshot Previous { get; init; } = null!;
    public TseHealthSnapshot Current { get; init; } = null!;
}
