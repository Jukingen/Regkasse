using System.Collections.Concurrent;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

public interface ITseTrainingConsoleStore
{
    IReadOnlyList<TseTrainingConsoleEntryDto> GetEntries(string userId, int take = 100);
    TseTrainingConsoleEntryDto Append(string userId, TseTrainingConsoleEntryDto entry);
    void Clear(string userId);
}

/// <summary>In-memory per-user simulation console (ring buffer). Not durable.</summary>
public sealed class TseTrainingConsoleStore : ITseTrainingConsoleStore
{
    private const int MaxPerUser = 200;
    private readonly ConcurrentDictionary<string, LinkedList<TseTrainingConsoleEntryDto>> _byUser = new(StringComparer.Ordinal);

    public IReadOnlyList<TseTrainingConsoleEntryDto> GetEntries(string userId, int take = 100)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Array.Empty<TseTrainingConsoleEntryDto>();

        take = Math.Clamp(take, 1, MaxPerUser);
        if (!_byUser.TryGetValue(userId, out var list))
            return Array.Empty<TseTrainingConsoleEntryDto>();

        lock (list)
        {
            return list.TakeLast(take).ToList();
        }
    }

    public TseTrainingConsoleEntryDto Append(string userId, TseTrainingConsoleEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required.", nameof(userId));

        var list = _byUser.GetOrAdd(userId, _ => new LinkedList<TseTrainingConsoleEntryDto>());
        lock (list)
        {
            list.AddLast(entry);
            while (list.Count > MaxPerUser)
                list.RemoveFirst();
        }

        return entry;
    }

    public void Clear(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;
        if (_byUser.TryGetValue(userId, out var list))
        {
            lock (list)
                list.Clear();
        }
    }
}
