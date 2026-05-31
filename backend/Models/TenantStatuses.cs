namespace KasseAPI_Final.Models;

/// <summary>Tenant lifecycle status stored in <see cref="Tenant.Status"/>.</summary>
public static class TenantStatuses
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Deleted = "deleted";

    /// <summary>
    /// In-memory comparisons (case-insensitive). Do not use inside EF LINQ — use <c>status != Deleted</c> instead.
    /// </summary>
    public static bool IsKnown(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Suspended, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Deleted, StringComparison.OrdinalIgnoreCase);
}
