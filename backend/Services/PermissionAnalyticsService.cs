using System.Text.Json;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PermissionAnalyticsService : IPermissionAnalyticsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly object CacheGate = new();
    private static PermissionAnalyticsSummaryDto? _cachedSummary;
    private static DateTime _cachedAtUtc = DateTime.MinValue;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEffectivePermissionResolver _effective;
    private readonly TimeProvider _time;

    public PermissionAnalyticsService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IEffectivePermissionResolver effective,
        TimeProvider time)
    {
        _db = db;
        _userManager = userManager;
        _effective = effective;
        _time = time;
    }

    public async Task<PermissionAnalyticsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        lock (CacheGate)
        {
            if (_cachedSummary is not null
                && _time.GetUtcNow().UtcDateTime - _cachedAtUtc < CacheTtl)
            {
                return _cachedSummary;
            }
        }

        var summary = await BuildSummaryAsync(cancellationToken);
        lock (CacheGate)
        {
            _cachedSummary = summary;
            _cachedAtUtc = _time.GetUtcNow().UtcDateTime;
        }

        return summary;
    }

    public async Task<IReadOnlyList<PermissionAnalyticsTrendPointDto>> GetTrendAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 366);
        var from = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime.Date.AddDays(-(days - 1)));
        var rows = await _db.PermissionUsageDaily.AsNoTracking()
            .Where(r => r.Date >= from)
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new PermissionAnalyticsTrendPointDto
        {
            Date = r.Date.ToString("yyyy-MM-dd"),
            TotalUsers = r.TotalUsers,
            PayloadJson = r.PayloadJson,
        }).ToList();
    }

    public async Task SnapshotTodayAsync(CancellationToken cancellationToken = default)
    {
        var summary = await BuildSummaryAsync(cancellationToken);
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var payload = JsonSerializer.Serialize(new
        {
            summary.MostUsed,
            summary.LeastUsed,
            summary.UnusedPermissions,
            summary.OverPrivilegedUsers,
            summary.RoleDistribution,
        });

        var existing = await _db.PermissionUsageDaily.FirstOrDefaultAsync(r => r.Date == today, cancellationToken);
        if (existing is null)
        {
            _db.PermissionUsageDaily.Add(new PermissionUsageDaily
            {
                Date = today,
                TotalUsers = summary.TotalUsers,
                PayloadJson = payload,
            });
        }
        else
        {
            existing.TotalUsers = summary.TotalUsers;
            existing.PayloadJson = payload;
        }

        await _db.SaveChangesAsync(cancellationToken);

        lock (CacheGate)
        {
            _cachedSummary = summary;
            _cachedAtUtc = _time.GetUtcNow().UtcDateTime;
        }
    }

    private async Task<PermissionAnalyticsSummaryDto> BuildSummaryAsync(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken);
        var permissionUserCounts = PermissionCatalog.All
            .ToDictionary(p => p, _ => 0, StringComparer.OrdinalIgnoreCase);
        var roleCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var overPrivileged = new List<(string UserId, string Label, int Extra)>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;

            // Direct grants only — no implication expansion (effective set from resolver already includes overrides).
            var effective = await _effective.GetEffectivePermissionsAsync(
                user.Id,
                roles,
                tenantId: null,
                cancellationToken);

            foreach (var perm in effective)
            {
                if (permissionUserCounts.ContainsKey(perm))
                    permissionUserCounts[perm]++;
                else
                    permissionUserCounts[perm] = 1;
            }

            var roleBaseline = 0;
            if (roles.Count > 0)
            {
                // Approximate baseline: SuperAdmin is always "privileged"; others use effective count vs median later.
                roleBaseline = string.Equals(
                    RoleCanonicalization.GetCanonicalRole(roles.FirstOrDefault() ?? string.Empty),
                    Roles.SuperAdmin,
                    StringComparison.Ordinal)
                    ? int.MaxValue / 4
                    : 25;
            }

            if (effective.Count > roleBaseline + 15
                && !roles.Any(r => string.Equals(
                    RoleCanonicalization.GetCanonicalRole(r),
                    Roles.SuperAdmin,
                    StringComparison.Ordinal)))
            {
                overPrivileged.Add((
                    user.Id,
                    user.UserName ?? user.Id,
                    effective.Count - roleBaseline));
            }
        }

        var totalUsers = Math.Max(1, users.Count);
        var ranked = permissionUserCounts
            .Select(kv => new PermissionAnalyticsNamedCountDto
            {
                Key = kv.Key,
                Label = kv.Key,
                UserCount = kv.Value,
                Percent = Math.Round(100.0 * kv.Value / totalUsers, 1),
            })
            .OrderByDescending(x => x.UserCount)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mostUsed = ranked.Where(r => r.UserCount > 0).Take(10).ToList();
        var leastUsed = ranked.Where(r => r.UserCount > 0).OrderBy(r => r.UserCount).Take(10).ToList();
        var unused = ranked.Where(r => r.UserCount == 0).Select(r => r.Key).OrderBy(k => k).ToList();

        var roleDistribution = roleCounts
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new PermissionAnalyticsNamedCountDto
            {
                Key = kv.Key,
                Label = kv.Key,
                UserCount = kv.Value,
                Percent = Math.Round(100.0 * kv.Value / totalUsers, 1),
            })
            .ToList();

        var overList = overPrivileged
            .OrderByDescending(x => x.Extra)
            .Take(20)
            .Select(x => new PermissionAnalyticsNamedCountDto
            {
                Key = x.UserId,
                Label = x.Label,
                UserCount = x.Extra,
                Percent = 0,
            })
            .ToList();

        var recommendations = new List<PermissionAnalyticsRecommendationDto>();
        if (unused.Count > 20)
        {
            recommendations.Add(new PermissionAnalyticsRecommendationDto
            {
                Code = "MANY_UNUSED_PERMISSIONS",
                Severity = "info",
                Message = "Many catalog permissions are unused by any user.",
                Arg = unused.Count.ToString(),
            });
        }

        if (overList.Count > 0)
        {
            recommendations.Add(new PermissionAnalyticsRecommendationDto
            {
                Code = "OVER_PRIVILEGED_USERS",
                Severity = "warning",
                Message = "Some users hold substantially more permissions than typical for their role.",
                Arg = overList.Count.ToString(),
            });
        }

        var roleCount = await _db.Roles.AsNoTracking().CountAsync(cancellationToken);

        return new PermissionAnalyticsSummaryDto
        {
            TotalUsers = users.Count,
            TotalRoles = roleCount,
            TotalPermissions = PermissionCatalog.All.Count,
            MostUsed = mostUsed,
            LeastUsed = leastUsed,
            RoleDistribution = roleDistribution,
            OverPrivilegedUsers = overList,
            UnusedPermissions = unused,
            Recommendations = recommendations,
        };
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(cancellationToken);
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        if (fmt is "pdf" or "json")
        {
            var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            return (bytes, "application/json", $"permission-analytics-{DateTime.UtcNow:yyyyMMdd}.json");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("section,key,label,userCount,percent");
        sb.AppendLine($"kpi,totalUsers,,{summary.TotalUsers},");
        sb.AppendLine($"kpi,totalRoles,,{summary.TotalRoles},");
        sb.AppendLine($"kpi,totalPermissions,,{summary.TotalPermissions},");
        foreach (var row in summary.MostUsed)
            sb.AppendLine($"mostUsed,{Escape(row.Key)},{Escape(row.Label)},{row.UserCount},{row.Percent:F1}");
        foreach (var row in summary.LeastUsed)
            sb.AppendLine($"leastUsed,{Escape(row.Key)},{Escape(row.Label)},{row.UserCount},{row.Percent:F1}");
        foreach (var row in summary.RoleDistribution)
            sb.AppendLine($"roleDistribution,{Escape(row.Key)},{Escape(row.Label)},{row.UserCount},{row.Percent:F1}");
        foreach (var unused in summary.UnusedPermissions)
            sb.AppendLine($"unused,{Escape(unused)},,0,0");
        foreach (var rec in summary.Recommendations)
            sb.AppendLine($"recommendation,{Escape(rec.Code)},{Escape(rec.Message)},,");

        return (
            System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"permission-analytics-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
