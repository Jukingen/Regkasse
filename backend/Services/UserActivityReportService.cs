using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class UserActivityReportService : IUserActivityReportService
{
    public const int DefaultTimelineLimit = 100;
    public const int MaxTimelineLimit = 500;
    private const int TopUsersLimit = 10;

    private static readonly HashSet<string> UserCreateActions = new(StringComparer.Ordinal)
    {
        AuditLogActions.USER_CREATE,
        AuditLogActions.USER_CREATED,
        AuditLogActions.TENANT_QUICK_USER_CREATED,
    };

    private static readonly HashSet<string> UserEditActions = new(StringComparer.Ordinal)
    {
        AuditLogActions.USER_UPDATE,
        AuditLogActions.USER_ROLE_CHANGE,
        AuditLogActions.USER_NAME_CHANGE,
        AuditLogActions.USER_TENANT_MEMBERSHIP_CHANGED,
        AuditLogActions.USER_DEACTIVATE,
        AuditLogActions.USER_REACTIVATE,
        AuditLogActions.USER_PASSWORD_RESET,
        AuditLogActions.FORCE_RESET_PASSWORD,
    };

    private static readonly HashSet<string> PaymentProcessedActions = new(StringComparer.Ordinal)
    {
        "PaymentCreated",
        AuditLogActions.PAYMENT_CONFIRM,
        AuditLogActions.POS_PAY_OUTCOME,
        AuditLogActions.PAYMENT_INITIATE,
    };

    private static readonly HashSet<string> StornoActions = new(StringComparer.Ordinal)
    {
        "PaymentReversal",
        AuditLogActions.PAYMENT_CANCEL,
    };

    private static readonly HashSet<string> RefundActions = new(StringComparer.Ordinal)
    {
        "PaymentRefunded",
        AuditLogActions.PAYMENT_REFUND,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<UserActivityReportService> _logger;

    public UserActivityReportService(AppDbContext db, ILogger<UserActivityReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<UserActivityReportDto?> BuildReportAsync(
        UserActivityReportQuery query,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken = default) =>
        BuildReportCoreAsync(query, actorIsSuperAdmin, ambientTenantId, cancellationToken);

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        UserActivityReportQuery query,
        string format,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken = default)
    {
        var exportQuery = new UserActivityReportQuery
        {
            UserId = query.UserId,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            ActionType = query.ActionType,
            IncludeTimeline = true,
            IncludeTopUsers = false,
            TimelineLimit = MaxTimelineLimit,
            DefaultRangeDays = query.DefaultRangeDays,
        };

        var report = await BuildReportCoreAsync(exportQuery, actorIsSuperAdmin, ambientTenantId, cancellationToken)
            .ConfigureAwait(false);
        if (report == null)
            throw new InvalidOperationException("User not found or not accessible.");

        return UserActivityReportExporter.Export(report, format);
    }

    private async Task<UserActivityReportDto?> BuildReportCoreAsync(
        UserActivityReportQuery query,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId?.Trim() ?? string.Empty;
        if (userId.Length == 0)
            return null;

        var timelineLimit = Math.Clamp(query.TimelineLimit, 1, MaxTimelineLimit);

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (user == null)
            return null;

        if (!actorIsSuperAdmin)
        {
            if (ambientTenantId is not Guid tenantId || tenantId == Guid.Empty)
                return null;

            var hasMembership = await _db.UserTenantMemberships.AsNoTracking()
                .AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!hasMembership)
                return null;
        }

        var (rangeStart, rangeEnd) = ResolveRange(query.FromDate, query.ToDate, query.DefaultRangeDays);
        var tenantScope = await ResolveTenantScopeAsync(userId, actorIsSuperAdmin, ambientTenantId, cancellationToken)
            .ConfigureAwait(false);
        var comparisonTenantIds = await ResolveComparisonTenantIdsAsync(
                actorIsSuperAdmin, ambientTenantId, tenantScope, cancellationToken)
            .ConfigureAwait(false);

        var auditBase = BuildAuditQuery(userId, tenantScope, rangeStart, rangeEnd, query.ActionType);
        var loginQuery = auditBase.Where(a => a.Action == AuditLogActions.USER_LOGIN);

        // Sequential EF queries — a single scoped DbContext must not run concurrent operations.
        var failedLoginAttempts = await loginQuery
            .CountAsync(a => a.Status != AuditLogStatus.Success, cancellationToken)
            .ConfigureAwait(false);
        var auditedSuccessfulLogins = await loginQuery
            .CountAsync(a => a.Status == AuditLogStatus.Success, cancellationToken)
            .ConfigureAwait(false);
        var lastLoginAudit = await loginQuery
            .Where(a => a.Status == AuditLogStatus.Success)
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new { a.Timestamp, a.IpAddress })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalActions = await auditBase.CountAsync(cancellationToken).ConfigureAwait(false);
        var actionRowList = await auditBase
            .GroupBy(a => new { a.Action, a.EntityType, a.Status })
            .Select(g => new { g.Key.Action, g.Key.EntityType, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dailyActivity = await auditBase
            .GroupBy(a => a.Timestamp.Date)
            .Select(g => new UserActivityDailyCountDto { Date = g.Key, Count = g.Count() })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activityTimeline = new List<UserActivityTimelineItemDto>();
        if (query.IncludeTimeline)
        {
            activityTimeline = await auditBase
                .OrderByDescending(a => a.Timestamp)
                .Take(timelineLimit)
                .Select(a => new UserActivityTimelineItemDto
                {
                    Date = a.Timestamp,
                    Action = a.Action,
                    EntityType = a.EntityType,
                    EntityId = a.EntityId,
                    IpAddress = a.IpAddress,
                    Status = a.Status.ToString(),
                    SessionId = a.SessionId,
                    CorrelationId = a.CorrelationId,
                    Description = a.Description,
                    TseSignature = a.TseSignature,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var topActiveUsers = new List<UserActivityRankingDto>();
        if (query.IncludeTopUsers && comparisonTenantIds.Count > 0)
        {
            topActiveUsers = await BuildTopActiveUsersAsync(
                    comparisonTenantIds, rangeStart, rangeEnd, query.ActionType, cancellationToken)
                .ConfigureAwait(false);
        }

        var sessionStats = await LoadSessionStatsAsync(userId, tenantScope, cancellationToken)
            .ConfigureAwait(false);
        var summary = SummarizeActions(
            actionRowList.Select(r => (r.Action, r.EntityType, r.Status, r.Count)));

        var tenantName = await ResolveTenantDisplayNameAsync(userId, tenantScope, cancellationToken)
            .ConfigureAwait(false);

        var lastLoginAt = user.LastLoginAt ?? user.LastLogin;
        var lastLoginIp = !string.IsNullOrWhiteSpace(lastLoginAudit?.IpAddress)
            ? lastLoginAudit!.IpAddress
            : sessionStats.LastIp;

        var totalLogins = user.LoginCount > 0 ? user.LoginCount : auditedSuccessfulLogins;

        _logger.LogInformation(
            "User activity report for {UserId}: {TotalActions} actions in {Days}d window",
            userId,
            totalActions,
            (rangeEnd - rangeStart).TotalDays);

        return new UserActivityReportDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = user.Role,
            TenantName = tenantName,
            FromDateUtc = rangeStart,
            ToDateUtc = rangeEnd,
            LastLoginAt = lastLoginAt,
            LastLoginIp = lastLoginIp,
            TotalLogins = totalLogins,
            FailedLoginAttempts = failedLoginAttempts,
            ActiveSessions = sessionStats.ActiveCount,
            AverageSessionDurationMinutes = sessionStats.AverageMinutes,
            LastSessionEndAt = sessionStats.LastEnd,
            TotalActions = totalActions,
            ActionsPerformed = summary,
            DailyActivity = dailyActivity,
            TopActiveUsers = topActiveUsers,
            ActivityTimeline = activityTimeline,
        };
    }

    private IQueryable<AuditLog> BuildAuditQuery(
        string userId,
        IReadOnlyList<Guid> tenantScope,
        DateTime rangeStart,
        DateTime rangeEnd,
        string? actionType)
    {
        // Explicit tenantScope below — bypass ambient fail-closed filter so SuperAdmin
        // (and tests without ICurrentTenantAccessor) can still aggregate by membership scope.
        var q = _db.AuditLogs.AsNoTracking().IgnoreQueryFilters().Where(a => a.UserId == userId);
        if (tenantScope.Count > 0)
            q = q.Where(a => tenantScope.Contains(a.TenantId));
        q = q.Where(a => a.Timestamp >= rangeStart && a.Timestamp < rangeEnd);
        if (!string.IsNullOrWhiteSpace(actionType))
            q = q.Where(a => a.Action == actionType.Trim());
        return q;
    }

    private static UserActivityActionSummaryDto SummarizeActions(
        IEnumerable<(string Action, string EntityType, AuditLogStatus Status, int Count)> rows)
    {
        var summary = new UserActivityActionSummaryDto();
        foreach (var row in rows)
        {
            if (UserCreateActions.Contains(row.Action))
                summary.UserCreates += row.Count;
            else if (UserEditActions.Contains(row.Action))
                summary.UserEdits += row.Count;
            else if (PaymentProcessedActions.Contains(row.Action) && row.Status == AuditLogStatus.Success)
                summary.PaymentsProcessed += row.Count;
            else if (StornoActions.Contains(row.Action))
                summary.Stornos += row.Count;
            else if (RefundActions.Contains(row.Action))
                summary.Refunds += row.Count;
            else if (row.EntityType == AuditLogEntityTypes.FISCAL_EXPORT
                     || row.Action.Contains("EXPORT", StringComparison.OrdinalIgnoreCase))
                summary.Exports += row.Count;
        }

        return summary;
    }

    private async Task<List<UserActivityRankingDto>> BuildTopActiveUsersAsync(
        IReadOnlyList<Guid> tenantIds,
        DateTime rangeStart,
        DateTime rangeEnd,
        string? actionType,
        CancellationToken cancellationToken)
    {
        var q = _db.AuditLogs.AsNoTracking().IgnoreQueryFilters()
            .Where(a => tenantIds.Contains(a.TenantId))
            .Where(a => a.Timestamp >= rangeStart && a.Timestamp < rangeEnd);
        if (!string.IsNullOrWhiteSpace(actionType))
            q = q.Where(a => a.Action == actionType.Trim());

        var ranked = await q
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(TopUsersLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ranked.Count == 0)
            return new List<UserActivityRankingDto>();

        var ids = ranked.Select(r => r.UserId).ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Role })
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        return ranked.Select(r =>
        {
            users.TryGetValue(r.UserId, out var u);
            return new UserActivityRankingDto
            {
                UserId = r.UserId,
                UserName = u?.UserName ?? r.UserId,
                Role = u?.Role ?? string.Empty,
                ActionCount = r.Count,
            };
        }).ToList();
    }

    private sealed record SessionStats(int ActiveCount, double AverageMinutes, DateTime? LastEnd, string? LastIp);

    private async Task<SessionStats> LoadSessionStatsAsync(
        string userId,
        IReadOnlyList<Guid> tenantScope,
        CancellationToken cancellationToken)
    {
        var sessionQuery = _db.AuthSessions.AsNoTracking().IgnoreQueryFilters().Where(s => s.UserId == userId);
        if (tenantScope.Count > 0)
            sessionQuery = sessionQuery.Where(s => s.TenantId == null || tenantScope.Contains(s.TenantId.Value));

        var activeCount = await sessionQuery
            .CountAsync(s => s.RevokedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);

        var endedSessions = await sessionQuery
            .Where(s => s.RevokedAtUtc != null)
            .Select(s => new { s.CreatedAtUtc, RevokedAtUtc = s.RevokedAtUtc!.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        double averageMinutes = 0;
        DateTime? lastEnd = null;
        if (endedSessions.Count > 0)
        {
            averageMinutes = endedSessions.Average(s => (s.RevokedAtUtc - s.CreatedAtUtc).TotalMinutes);
            lastEnd = endedSessions.Max(s => s.RevokedAtUtc);
        }

        var lastIp = await sessionQuery
            .OrderByDescending(s => s.LastActivityAtUtc ?? s.CreatedAtUtc)
            .Select(s => s.IpAddress)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SessionStats(activeCount, Math.Round(averageMinutes, 1), lastEnd, lastIp);
    }

    private static (DateTime Start, DateTime End) ResolveRange(
        DateTime? startDate,
        DateTime? endDate,
        int defaultRangeDays)
    {
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
        var end = hi ?? DateTime.UtcNow.Date.AddDays(1);
        var days = Math.Clamp(defaultRangeDays, 1, 366);
        var start = lo ?? end.AddDays(-days);
        if (start >= end)
            start = end.AddDays(-days);
        return (start, end);
    }

    private async Task<List<Guid>> ResolveTenantScopeAsync(
        string userId,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken)
    {
        if (!actorIsSuperAdmin && ambientTenantId is Guid tid)
            return new List<Guid> { tid };

        return await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<Guid>> ResolveComparisonTenantIdsAsync(
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        IReadOnlyList<Guid> userTenantScope,
        CancellationToken cancellationToken)
    {
        if (!actorIsSuperAdmin && ambientTenantId is Guid tid)
            return new List<Guid> { tid };

        if (userTenantScope.Count > 0)
            return userTenantScope.ToList();

        if (actorIsSuperAdmin && ambientTenantId is Guid ambient && ambient != Guid.Empty)
            return new List<Guid> { ambient };

        return await _db.UserTenantMemberships.AsNoTracking()
            .Where(m => m.IsActive)
            .Select(m => m.TenantId)
            .Distinct()
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> ResolveTenantDisplayNameAsync(
        string userId,
        IReadOnlyList<Guid> tenantScope,
        CancellationToken cancellationToken)
    {
        if (tenantScope.Count == 0)
            return string.Empty;

        if (tenantScope.Count == 1)
        {
            var name = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantScope[0])
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return name ?? string.Empty;
        }

        var names = await _db.Tenants.AsNoTracking()
            .Where(t => tenantScope.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => t.Name)
            .Take(3)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (names.Count == 0)
            return string.Empty;

        var suffix = tenantScope.Count > names.Count ? $" (+{tenantScope.Count - names.Count})" : string.Empty;
        return string.Join(", ", names) + suffix;
    }
}
