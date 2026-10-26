using System.Collections;
using System.Runtime.CompilerServices;
using Sentry;

namespace KasseAPI_Final.Services.Monitoring;

/// <summary>
/// Drops credential and fiscal secret fields before an event leaves the process.
/// Keeps operator identifiers (userId, tenantId, endpoint, method, status).
/// </summary>
internal static class SentryPiiScrubber
{
    internal const double DefaultTracesSampleRate = 0.1;

    private static readonly HashSet<string> DroppedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "password",
        "token",
        "voucherCode",
        "TseSignature",
    };

    private static readonly ConditionalWeakTable<Exception, object> Captured = new();

    internal static SentryEvent? BeforeSend(SentryEvent sentryEvent, SentryHint hint)
    {
        _ = hint;
        if (IsIgnoredRequest(sentryEvent.Request?.Url))
            return null;
        if (sentryEvent.Exception is { } exception && !Captured.TryAdd(exception, Captured))
            return null;

        Scrub(sentryEvent);
        return sentryEvent;
    }

    internal static SentryTransaction? BeforeSendTransaction(SentryTransaction transaction, SentryHint hint)
    {
        _ = hint;
        if (IsIgnoredRequest(transaction.Request?.Url))
            return null;
        return transaction;
    }

    /// <summary>Health probes and the Prometheus scrape must not become Sentry transactions or errors.</summary>
    internal static bool IsIgnoredPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var value = path.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            value = absolute.AbsolutePath;

        if (!value.StartsWith('/'))
            value = "/" + value;

        return value.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/metrics", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/metrics/", StringComparison.OrdinalIgnoreCase);
    }

    internal static void Scrub(SentryEvent sentryEvent)
    {
        ScrubObject(sentryEvent.Extra);
        ScrubObject(sentryEvent.Tags);
        if (sentryEvent.User != null)
        {
            sentryEvent.User.Email = null;
            ScrubDictionary(sentryEvent.User.Other);
        }

        if (sentryEvent.Request != null)
            ScrubRequest(sentryEvent.Request);

        if (sentryEvent.Contexts is IEnumerable<KeyValuePair<string, object>> contexts)
        {
            foreach (var pair in contexts)
                ScrubObject(pair.Value);
        }
    }

    private static bool IsIgnoredRequest(string? url) => IsIgnoredPath(url);

    private static void ScrubRequest(SentryRequest request)
    {
        ScrubObject(request.Data);
        ScrubObject(request.Env);
        ScrubObject(request.Other);
        ScrubDictionary(request.Headers);
        request.QueryString = StripQuery(request.QueryString);
        request.Cookies = StripCookieHeader(request.Cookies);
    }

    private static void ScrubObject(object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string:
                return;
            case IDictionary<string, object?> objects:
                ScrubDictionary(objects);
                return;
            case IDictionary<string, string> strings:
                ScrubDictionary(strings);
                return;
            case IDictionary dictionary:
                ScrubNonGeneric(dictionary);
                return;
        }
    }

    private static void ScrubDictionary<TValue>(IDictionary<string, TValue>? dictionary)
    {
        if (dictionary == null || dictionary.Count == 0)
            return;

        foreach (var key in dictionary.Keys.Where(IsDroppedKey).ToArray())
            dictionary.Remove(key);

        foreach (var key in dictionary.Keys.ToArray())
        {
            if (dictionary[key] is object nested && nested is not string)
                ScrubObject(nested);
        }
    }

    private static void ScrubNonGeneric(IDictionary dictionary)
    {
        var dropped = new List<object>();
        foreach (var key in dictionary.Keys)
        {
            if (key is string name && IsDroppedKey(name))
                dropped.Add(key);
        }

        foreach (var key in dropped)
            dictionary.Remove(key);

        foreach (var key in dictionary.Keys)
        {
            if (dictionary[key] is object nested && nested is not string)
                ScrubObject(nested);
        }
    }

    private static bool IsDroppedKey(string key) => DroppedKeys.Contains(key);

    private static string? StripQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return query;

        var raw = query.StartsWith('?') ? query[1..] : query;
        var kept = raw.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair =>
            {
                var name = pair.Split('=', 2)[0];
                return !IsDroppedKey(Uri.UnescapeDataString(name));
            });
        var joined = string.Join('&', kept);
        if (joined.Length == 0)
            return query.StartsWith('?') ? "?" : string.Empty;
        return query.StartsWith('?') ? "?" + joined : joined;
    }

    private static string? StripCookieHeader(string? cookies)
    {
        if (string.IsNullOrEmpty(cookies))
            return cookies;

        var kept = cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(pair =>
            {
                var name = pair.Split('=', 2)[0];
                return !IsDroppedKey(name);
            });
        return string.Join("; ", kept);
    }
}
