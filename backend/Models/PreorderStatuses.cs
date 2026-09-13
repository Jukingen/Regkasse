namespace KasseAPI_Final.Models;

/// <summary>Operational pickup lifecycle for paid POS Vorbestellungen. Not a fiscal receipt status.</summary>
public static class PreorderStatuses
{
    public const string Pending = "pending";
    public const string Ready = "ready";
    public const string Collected = "collected";
    public const string Cancelled = "cancelled";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Ready,
        Collected,
        Cancelled
    };

    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return All.Contains(v) ? v : Pending;
    }
}
