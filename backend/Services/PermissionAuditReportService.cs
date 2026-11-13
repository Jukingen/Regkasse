using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Reports;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PermissionAuditReportFilters
{
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? PermissionKey { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class PermissionAuditNamedCountDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class PermissionAuditDailyCountDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class PermissionAuditReportDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int TotalChanges { get; set; }
    public Dictionary<string, int> ByAction { get; set; } = new();
    public List<PermissionAuditDailyCountDto> ByDate { get; set; } = new();
    public List<PermissionAuditNamedCountDto> TopActors { get; set; } = new();
    public List<PermissionAuditNamedCountDto> TopPermissions { get; set; } = new();
    public List<PermissionAuditNamedCountDto> TopRoles { get; set; } = new();
    public int CriticalCount { get; set; }
    public int UniqueActors { get; set; }
    public int UniquePermissions { get; set; }
}

public sealed class PermissionAccessRowDto
{
    public string SubjectType { get; set; } = "role"; // role | user
    public string SubjectId { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public string AccessState { get; set; } = "allowed"; // allowed | denied | override_grant | override_deny
    public DateTime? LastReviewedAtUtc { get; set; }
    public bool IsStale { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsExpired { get; set; }
}

public sealed class PermissionComplianceDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int StaleDaysThreshold { get; set; }
    public DateTime? LastPermissionReviewAtUtc { get; set; }
    public int RolePermissionCount { get; set; }
    public int ActiveOverrideCount { get; set; }
    public int ExpiredOverrideCount { get; set; }
    public int StaleSubjectCount { get; set; }
    public List<PermissionAccessRowDto> AccessMatrix { get; set; } = new();
    public List<PermissionAccessRowDto> ExpiredOrStale { get; set; } = new();
}

public sealed class PermissionAuditExportResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "permission-audit.bin";
}

public interface IPermissionAuditReportService
{
    Task<PermissionAuditReportDto> GetReportAsync(
        PermissionAuditReportFilters filters,
        CancellationToken cancellationToken = default);

    Task<PermissionAuditExportResult> ExportAsync(
        PermissionAuditReportFilters filters,
        string format,
        CancellationToken cancellationToken = default);

    Task<PermissionComplianceDto> GetComplianceAsync(
        int staleDays = 90,
        CancellationToken cancellationToken = default);
}

public static class PermissionAuditScheduleFormats
{
    public const string Csv = "permission-csv";
    public const string Json = "permission-json";
    public const string Pdf = "permission-pdf";

    public static bool IsPermissionFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return false;
        var f = format.Trim().ToLowerInvariant();
        return f is Csv or Json or Pdf;
    }
}

public sealed class PermissionAuditReportService : IPermissionAuditReportService
{
    private const int DefaultStaleDays = 90;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly IPermissionAuditService _permissionAudit;
    private readonly IRoleManagementService _roleManagementService;
    private readonly IAuditLogService _auditLogService;
    private readonly AppDbContext _db;

    public PermissionAuditReportService(
        IPermissionAuditService permissionAudit,
        IRoleManagementService roleManagementService,
        IAuditLogService auditLogService,
        AppDbContext db)
    {
        _permissionAudit = permissionAudit;
        _roleManagementService = roleManagementService;
        _auditLogService = auditLogService;
        _db = db;
    }

    public async Task<PermissionAuditReportDto> GetReportAsync(
        PermissionAuditReportFilters filters,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = NormalizeRange(filters.FromDate, filters.ToDate);
        var entries = await _permissionAudit.CollectEntriesAsync(
            filters.RoleId,
            filters.RoleName,
            filters.PermissionKey,
            filters.ActorUserId,
            from,
            to,
            maxRows: 10_000,
            cancellationToken).ConfigureAwait(false);

        var byAction = entries
            .GroupBy(e => e.Action, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var byDate = entries
            .GroupBy(e => e.Timestamp.ToUniversalTime().Date)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionAuditDailyCountDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count(),
            })
            .ToList();

        var topActors = entries
            .GroupBy(e => string.IsNullOrWhiteSpace(e.ActorUserId) ? "system" : e.ActorUserId)
            .Select(g =>
            {
                var sample = g.First();
                var label = string.IsNullOrWhiteSpace(sample.ActorName)
                    ? (string.IsNullOrWhiteSpace(sample.ActorEmail) ? g.Key : sample.ActorEmail)
                    : sample.ActorName;
                return new PermissionAuditNamedCountDto
                {
                    Key = g.Key,
                    Label = label,
                    Count = g.Count(),
                };
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var topPermissions = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.PermissionKey))
            .GroupBy(e => e.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PermissionAuditNamedCountDto
            {
                Key = g.Key,
                Label = g.Key,
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();

        var topRoles = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.RoleName))
            .GroupBy(e => e.RoleName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PermissionAuditNamedCountDto
            {
                Key = g.Key,
                Label = g.Key,
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var critical = entries.Count(e =>
            string.Equals(e.Action, "deleted", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.RoleName, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.PermissionKey, AppPermissions.SystemCritical, StringComparison.OrdinalIgnoreCase));

        return new PermissionAuditReportDto
        {
            FromUtc = from,
            ToUtc = to,
            TotalChanges = entries.Count,
            ByAction = byAction,
            ByDate = byDate,
            TopActors = topActors,
            TopPermissions = topPermissions,
            TopRoles = topRoles,
            CriticalCount = critical,
            UniqueActors = entries
                .Select(e => e.ActorUserId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            UniquePermissions = entries
                .Select(e => e.PermissionKey)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
        };
    }

    public async Task<PermissionAuditExportResult> ExportAsync(
        PermissionAuditReportFilters filters,
        string format,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeExportFormat(format);
        var (from, to) = NormalizeRange(filters.FromDate, filters.ToDate);
        filters.FromDate = from;
        filters.ToDate = to;

        var report = await GetReportAsync(filters, cancellationToken).ConfigureAwait(false);
        var entries = await _permissionAudit.CollectEntriesAsync(
            filters.RoleId,
            filters.RoleName,
            filters.PermissionKey,
            filters.ActorUserId,
            from,
            to,
            maxRows: 10_000,
            cancellationToken).ConfigureAwait(false);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return normalized switch
        {
            "json" => new PermissionAuditExportResult
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    report,
                    entries,
                }, JsonOptions)),
                ContentType = "application/json; charset=utf-8",
                FileName = $"permission-audit-{stamp}.json",
            },
            "pdf" => new PermissionAuditExportResult
            {
                Content = PermissionAuditReportPdfGenerator.Generate(report, entries.Take(200).ToList()),
                ContentType = "application/pdf",
                FileName = $"permission-audit-{stamp}.pdf",
            },
            _ => new PermissionAuditExportResult
            {
                Content = Encoding.UTF8.GetBytes(BuildCsv(entries)),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"permission-audit-{stamp}.csv",
            },
        };
    }

    public async Task<PermissionComplianceDto> GetComplianceAsync(
        int staleDays = DefaultStaleDays,
        CancellationToken cancellationToken = default)
    {
        staleDays = Math.Clamp(staleDays, 7, 365);
        var now = DateTime.UtcNow;
        var staleBefore = now.AddDays(-staleDays);

        var roles = await _roleManagementService.GetRolesWithPermissionsAsync(cancellationToken)
            .ConfigureAwait(false);
        var lastRoleReviews = await LoadLastRoleReviewMapAsync(cancellationToken).ConfigureAwait(false);

        var access = new List<PermissionAccessRowDto>();
        foreach (var role in roles)
        {
            lastRoleReviews.TryGetValue(role.RoleName, out var lastReview);
            var isStale = lastReview == default || lastReview < staleBefore;
            foreach (var permission in role.Permissions ?? Array.Empty<string>())
            {
                access.Add(new PermissionAccessRowDto
                {
                    SubjectType = "role",
                    SubjectId = role.RoleKey ?? role.RoleName,
                    SubjectName = role.DisplayName ?? role.RoleName,
                    PermissionKey = permission,
                    AccessState = "allowed",
                    LastReviewedAtUtc = lastReview == default ? null : lastReview,
                    IsStale = isStale,
                });
            }
        }

        var overrides = await _db.UserPermissionOverrides.AsNoTracking()
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var expiredCount = 0;
        var activeOverrideCount = 0;
        foreach (var o in overrides)
        {
            var expired = o.ExpiresAt.HasValue && o.ExpiresAt.Value <= now;
            if (expired) expiredCount++;
            else activeOverrideCount++;

            var subjectName = o.User?.UserName
                ?? o.User?.Email
                ?? o.UserId;
            var lastReview = o.CreatedAt;
            var isStale = !expired && lastReview < staleBefore;

            access.Add(new PermissionAccessRowDto
            {
                SubjectType = "user",
                SubjectId = o.UserId,
                SubjectName = subjectName ?? o.UserId,
                PermissionKey = o.Permission,
                AccessState = o.IsGranted ? "override_grant" : "override_deny",
                LastReviewedAtUtc = lastReview,
                IsStale = isStale,
                ExpiresAtUtc = o.ExpiresAt,
                IsExpired = expired,
            });
        }

        DateTime? lastAnyReview = null;
        if (lastRoleReviews.Count > 0 || overrides.Count > 0)
        {
            var candidates = lastRoleReviews.Values
                .Concat(overrides.Select(o => o.CreatedAt))
                .ToList();
            if (candidates.Count > 0)
                lastAnyReview = candidates.Max();
        }

        var expiredOrStale = access
            .Where(a => a.IsExpired || a.IsStale)
            .OrderByDescending(a => a.IsExpired)
            .ThenBy(a => a.LastReviewedAtUtc)
            .Take(200)
            .ToList();

        var staleSubjects = access
            .Where(a => a.IsStale || a.IsExpired)
            .Select(a => $"{a.SubjectType}:{a.SubjectId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new PermissionComplianceDto
        {
            GeneratedAtUtc = now,
            StaleDaysThreshold = staleDays,
            LastPermissionReviewAtUtc = lastAnyReview,
            RolePermissionCount = access.Count(a => a.SubjectType == "role"),
            ActiveOverrideCount = activeOverrideCount,
            ExpiredOverrideCount = expiredCount,
            StaleSubjectCount = staleSubjects,
            AccessMatrix = access
                .OrderBy(a => a.SubjectType)
                .ThenBy(a => a.SubjectName)
                .ThenBy(a => a.PermissionKey)
                .Take(500)
                .ToList(),
            ExpiredOrStale = expiredOrStale,
        };
    }

    private async Task<Dictionary<string, DateTime>> LoadLastRoleReviewMapAsync(CancellationToken cancellationToken)
    {
        var filters = new AuditLogQueryFilters
        {
            StartDate = DateTime.UtcNow.AddYears(-2),
            EntityType = AuditLogEntityTypes.ROLE,
            Action = AuditLogActions.ROLE_PERMISSIONS_UPDATE,
        };

        var map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; page <= 20; page++)
        {
            var (items, _) = await _auditLogService.GetAuditLogsPagedAsync(
                filters, pageSize: 100, page: page, includeTotalCount: false).ConfigureAwait(false);
            if (items.Count == 0)
                break;

            foreach (var log in items)
            {
                var name = log.EntityName;
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (!map.TryGetValue(name, out var existing) || log.Timestamp > existing)
                    map[name] = log.Timestamp;
            }
        }

        return map;
    }

    private static (DateTime From, DateTime To) NormalizeRange(DateTime? from, DateTime? to)
    {
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = from?.ToUniversalTime() ?? end.AddDays(-30);
        if (start > end)
            start = end.AddDays(-30);
        return (start, end);
    }

    private static string NormalizeExportFormat(string format)
    {
        var f = (format ?? "csv").Trim().ToLowerInvariant();
        return f switch
        {
            "permission-json" or "json" => "json",
            "permission-pdf" or "pdf" => "pdf",
            _ => "csv",
        };
    }

    private static string BuildCsv(IReadOnlyList<PermissionAuditEntryDto> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp_utc,action,actor_user_id,actor_name,actor_email,role_id,role_name,permission_key,old_value,new_value,reason,ip_address,id");
        foreach (var e in entries)
        {
            sb.Append(Csv(e.Timestamp.ToUniversalTime().ToString("o"))).Append(',');
            sb.Append(Csv(e.Action)).Append(',');
            sb.Append(Csv(e.ActorUserId)).Append(',');
            sb.Append(Csv(e.ActorName)).Append(',');
            sb.Append(Csv(e.ActorEmail)).Append(',');
            sb.Append(Csv(e.RoleId)).Append(',');
            sb.Append(Csv(e.RoleName)).Append(',');
            sb.Append(Csv(e.PermissionKey)).Append(',');
            sb.Append(Csv(e.OldValue)).Append(',');
            sb.Append(Csv(e.NewValue)).Append(',');
            sb.Append(Csv(e.Reason)).Append(',');
            sb.Append(Csv(e.IpAddress)).Append(',');
            sb.Append(Csv(e.Id.ToString("D")));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
