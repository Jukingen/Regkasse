namespace KasseAPI_Final.Models;

/// <summary>
/// Operational mode stored in <see cref="Tenant.OperationMode"/> (orthogonal to lifecycle <see cref="Tenant.Status"/>).
/// </summary>
public static class TenantOperationModes
{
    public const string Active = "active";
    public const string Readonly = "readonly";
    public const string Maintenance = "maintenance";

    public static bool IsKnown(string? mode) =>
        string.Equals(mode, Active, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, Readonly, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, Maintenance, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? mode) =>
        IsKnown(mode) ? mode!.Trim().ToLowerInvariant() : Active;

    /// <summary>
    /// True when the tenant is currently in an effective maintenance window.
    /// Expired <paramref name="endsAt"/> is treated as not in maintenance.
    /// </summary>
    public static bool IsMaintenanceActive(
        string? operationMode,
        DateTime? endsAtUtc,
        DateTime utcNow)
    {
        if (!string.Equals(operationMode, Maintenance, StringComparison.OrdinalIgnoreCase))
            return false;
        if (endsAtUtc is DateTime end && end <= utcNow)
            return false;
        return true;
    }
}
