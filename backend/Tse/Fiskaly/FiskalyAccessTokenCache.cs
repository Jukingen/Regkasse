using System.Collections.Concurrent;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>Per-tenant fiskaly bearer cache. A null tenant id is the Development host slot.</summary>
public sealed class FiskalyAccessTokenCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool TryGet(Guid? tenantId, out string token, out DateTimeOffset expiresAt)
    {
        if (_entries.TryGetValue(ScopeKey(tenantId), out var entry)
            && !string.IsNullOrWhiteSpace(entry.AccessToken)
            && DateTimeOffset.UtcNow < entry.ExpiresAt.AddMinutes(-1))
        {
            token = entry.AccessToken;
            expiresAt = entry.ExpiresAt;
            return true;
        }

        token = string.Empty;
        expiresAt = DateTimeOffset.MinValue;
        return false;
    }

    public void Set(Guid? tenantId, string token, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _entries[ScopeKey(tenantId)] = new Entry(token, expiresAt);
    }

    public void Clear(Guid? tenantId)
    {
        _entries.TryRemove(ScopeKey(tenantId), out _);
    }

    public static string ScopeKey(Guid? tenantId) => tenantId?.ToString("D") ?? "host";

    private sealed record Entry(string AccessToken, DateTimeOffset ExpiresAt);
}
