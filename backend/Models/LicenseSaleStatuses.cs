namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>license_sales.status</c> values.</summary>
public static class LicenseSaleStatuses
{
    public const string Active = "active";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Active,
        Cancelled,
        Refunded,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
