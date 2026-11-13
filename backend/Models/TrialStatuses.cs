namespace KasseAPI_Final.Models;

/// <summary>SaaS trial lifecycle values stored on <see cref="Tenant.TrialStatus"/>.</summary>
public static class TrialStatuses
{
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Converted = "converted";
    public const string Deleted = "deleted";

    public static bool IsKnown(string? value) =>
        string.Equals(value, Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Expired, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Converted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Deleted, StringComparison.OrdinalIgnoreCase);

    public static bool IsOpenTrial(string? value) =>
        string.Equals(value, Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Expired, StringComparison.OrdinalIgnoreCase);
}
