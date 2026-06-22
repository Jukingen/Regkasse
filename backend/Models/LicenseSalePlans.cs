namespace KasseAPI_Final.Models;

/// <summary>Allowed <c>license_sales.license_plan</c> values (Mandanten SaaS billing).</summary>
public static class LicenseSalePlans
{
    public const string SixMonths = "6_months";
    public const string TwelveMonths = "12_months";
    public const string Custom = "custom";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        SixMonths,
        TwelveMonths,
        Custom,
    };

    public static bool IsValid(string? plan) =>
        !string.IsNullOrWhiteSpace(plan) && All.Contains(plan.Trim());
}
