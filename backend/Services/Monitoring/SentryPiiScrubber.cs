using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Sentry;

namespace KasseAPI_Final.Services.Monitoring;

/// <summary>
/// Drops credential and fiscal secret fields before an event leaves the process.
/// Keeps operator identifiers (userId, tenantId, endpoint, method, status).
/// </summary>
internal static class SentryPiiScrubber
{
    internal const double DefaultTracesSampleRate = SentrySampling.DefaultTracesSampleRate;

    private static readonly HashSet<string> DroppedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "password",
        "token",
        "voucherCode",
        "TseSignature",
        "phone",
        "name",
        "address",
        "iban",
        "authorization",
        "exception_message",
        "breadcrumb",
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

        ScrubTransaction(transaction);
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

        RedactMessage(sentryEvent.Message);
        RedactExceptions(sentryEvent.SentryExceptions);
        RedactBreadcrumbs(sentryEvent.Breadcrumbs);
    }

    private static void ScrubTransaction(SentryTransaction transaction)
    {
        if (transaction.User != null)
        {
            transaction.User.Email = null;
            transaction.User.Username = null;
            transaction.User.IpAddress = null;
            ScrubDictionary(transaction.User.Other);
        }

        if (transaction.Request != null)
            ScrubRequest(transaction.Request);

        ScrubObject(transaction.Data);
        ScrubObject(transaction.Tags);
        transaction.Description = RedactEmails(transaction.Description);
        RedactBreadcrumbs(transaction.Breadcrumbs);
    }

    private static void RedactMessage(SentryMessage? message)
    {
        if (message == null)
            return;
        message.Message = RedactEmails(message.Message);
        message.Formatted = RedactEmails(message.Formatted);
    }

    private static void RedactExceptions(IEnumerable<Sentry.Protocol.SentryException>? exceptions)
    {
        if (exceptions == null)
            return;
        foreach (var exception in exceptions)
            exception.Value = RedactEmails(exception.Value);
    }

    private static void RedactBreadcrumbs(IEnumerable<Breadcrumb>? breadcrumbs)
    {
        // Breadcrumb.Message and Data are init-only. Rebuild the mutable list the SDK stores.
        if (breadcrumbs is not List<Breadcrumb> list || list.Count == 0)
            return;

        var rebuilt = new List<Breadcrumb>(list.Count);
        foreach (var breadcrumb in list)
        {
            rebuilt.Add(new Breadcrumb(
                RedactEmails(breadcrumb.Message) ?? string.Empty,
                breadcrumb.Type ?? "default",
                ScrubBreadcrumbData(breadcrumb.Data),
                breadcrumb.Category,
                breadcrumb.Level));
        }

        list.Clear();
        list.AddRange(rebuilt);
    }

    private static Dictionary<string, string>? ScrubBreadcrumbData(IReadOnlyDictionary<string, string>? data)
    {
        if (data == null || data.Count == 0)
            return null;

        var copy = new Dictionary<string, string>(data.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in data)
        {
            if (IsDroppedKey(pair.Key))
                continue;
            copy[pair.Key] = RedactEmails(pair.Value) ?? string.Empty;
        }

        return copy.Count == 0 ? null : copy;
    }

    private static string? RedactEmails(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('@', StringComparison.Ordinal))
            return value;
        return EmailInText.Replace(value, "***");
    }

    private static readonly Regex EmailInText = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
