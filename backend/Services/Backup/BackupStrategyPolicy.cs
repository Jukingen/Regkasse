using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Defaults and dump table-exclude rules for <see cref="BackupStrategyKind"/>.
/// Does not implement row-level tenant filtering (shared PostgreSQL instance).
/// </summary>
public static class BackupStrategyPolicy
{
    public const int TenantRetentionDays = 30;
    public const int SystemRetentionDays = 90;

    /// <summary>AspNet Identity tables — excluded from Tenant strategy dumps; included for System.</summary>
    public static readonly string[] IdentityExcludeTables =
    {
        "AspNetUsers",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens"
    };

    public static BackupStrategyKind Resolve(Guid? tenantId, BackupStrategyKind? explicitStrategy = null)
    {
        if (explicitStrategy.HasValue)
            return explicitStrategy.Value;

        return tenantId is Guid tid && tid != Guid.Empty
            ? BackupStrategyKind.Tenant
            : BackupStrategyKind.System;
    }

    public static int DefaultRetentionDays(BackupStrategyKind strategy) =>
        strategy == BackupStrategyKind.Tenant ? TenantRetentionDays : SystemRetentionDays;

    /// <summary>
    /// Tenant: always exclude Identity (cost + credential hygiene).
    /// System: include Identity — ignore config exclude list for those tables so Super Admin DR has credentials.
    /// Other configured excludes still apply for System.
    /// </summary>
    public static IReadOnlyList<string> ResolveExcludeTables(
        BackupStrategyKind strategy,
        BackupOptions options)
    {
        var configured = (options.LogicalDumpExcludeTables ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (strategy == BackupStrategyKind.Tenant)
        {
            foreach (var table in IdentityExcludeTables)
            {
                if (!configured.Contains(table, StringComparer.OrdinalIgnoreCase))
                    configured.Add(table);
            }

            return configured;
        }

        // System: strip Identity excludes so credentials are in the dump.
        return configured
            .Where(t => !IdentityExcludeTables.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    public static int ResolveRetentionCutoffDays(
        BackupStrategyKind strategy,
        int tenantScheduleRetentionDays,
        int systemScheduleRetentionDays)
    {
        if (strategy == BackupStrategyKind.Tenant)
        {
            return Math.Clamp(
                tenantScheduleRetentionDays > 0 ? tenantScheduleRetentionDays : TenantRetentionDays,
                7,
                90);
        }

        return Math.Clamp(
            systemScheduleRetentionDays > 0 ? systemScheduleRetentionDays : SystemRetentionDays,
            7,
            90);
    }
}
