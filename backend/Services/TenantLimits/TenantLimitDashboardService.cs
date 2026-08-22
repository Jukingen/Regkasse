using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Limits;

public sealed class TenantLimitDashboardService : ITenantLimitDashboardService
{
    private const int RecentLogLimit = 20;

    private readonly AppDbContext _db;
    private readonly ITenantLimitGuard _guard;

    public TenantLimitDashboardService(AppDbContext db, ITenantLimitGuard guard)
    {
        _db = db;
        _guard = guard;
    }

    public Task<LimitDashboardDto> GetDashboardAsync(
        Guid tenantId,
        string? readerUserId,
        CancellationToken cancellationToken = default) =>
        BuildAsync([tenantId], allTenants: false, readerUserId, cancellationToken);

    public async Task<LimitDashboardDto> GetDashboardForAllTenantsAsync(
        string? readerUserId,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.DeletedAtUtc == null && t.Status == TenantStatuses.Active)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await BuildAsync(tenantIds, allTenants: true, readerUserId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<LimitDashboardDto> BuildAsync(
        IReadOnlyList<Guid> tenantIds,
        bool allTenants,
        string? readerUserId,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return new LimitDashboardDto
            {
                LastUpdated = DateTime.UtcNow,
                AllTenants = allTenants,
            };
        }

        var names = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name, t.Slug })
            .ToDictionaryAsync(t => t.Id, t => (t.Name, t.Slug), cancellationToken)
            .ConfigureAwait(false);

        var changeCounts = await LoadChangeCountsAsync(tenantIds, cancellationToken).ConfigureAwait(false);
        var maxTickets = await LoadMaxTicketTodayAsync(tenantIds, cancellationToken).ConfigureAwait(false);

        var statuses = new List<LimitStatusDto>();
        foreach (var tenantId in tenantIds)
        {
            names.TryGetValue(tenantId, out var label);
            var usage = await _guard.GetUsageAsync(tenantId, cancellationToken).ConfigureAwait(false);
            changeCounts.TryGetValue(tenantId, out var tenantChanges);
            maxTickets.TryGetValue(tenantId, out var maxTicket);
            statuses.AddRange(LimitDashboardMapper.FromUsage(
                usage,
                label.Name,
                maxTicket,
                tenantChanges,
                label.Slug));
        }

        var criticalUsers = await LoadCriticalUsersAsync(tenantIds, names, statuses, cancellationToken)
            .ConfigureAwait(false);
        var recentActivity = await LoadRecentActivityAsync(tenantIds, names, readerUserId, cancellationToken)
            .ConfigureAwait(false);
        var unreadAlertCount = await CountUnreadAlertsAsync(tenantIds, readerUserId, cancellationToken)
            .ConfigureAwait(false);

        var healthy = statuses.Count(s => s.Status == LimitUsageStatuses.Healthy);
        var warning = statuses.Count(s => s.Status == LimitUsageStatuses.Warning);
        var critical = statuses.Count(s => s.Status == LimitUsageStatuses.Critical);
        var approachingUsers = criticalUsers.Count(u => u.Status == LimitUsageStatuses.Approaching);
        var exceededUsers = criticalUsers.Count(u =>
            u.Status is LimitUsageStatuses.Exceeded or LimitUsageStatuses.Full);

        return new LimitDashboardDto
        {
            LastUpdated = DateTime.UtcNow,
            Summary = new DashboardSummaryDto
            {
                Total = statuses.Count,
                Healthy = healthy,
                Warning = warning,
                Critical = critical,
            },
            Limits = statuses,
            CriticalUsers = criticalUsers,
            RecentActivity = recentActivity,
            TotalViolations = critical + exceededUsers,
            ApproachingLimits = warning + approachingUsers,
            UnreadAlertCount = unreadAlertCount,
            AllTenants = allTenants,
        };
    }

    private async Task<IReadOnlyList<CriticalUserDto>> LoadCriticalUsersAsync(
        IReadOnlyList<Guid> tenantIds,
        IReadOnlyDictionary<Guid, (string Name, string Slug)> names,
        IReadOnlyList<LimitStatusDto> statuses,
        CancellationToken cancellationToken)
    {
        var assignments = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId)
                        && r.AssignedUserId != null
                        && r.Status != RegisterStatus.Decommissioned)
            .GroupBy(r => new { r.TenantId, r.AssignedUserId })
            .Select(g => new { g.Key.TenantId, UserId = g.Key.AssignedUserId!, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (assignments.Count == 0)
            return [];

        var capsByTenant = statuses
            .Where(s => s.Key == TenantLimitKeys.MaxActiveRegistersPerUser)
            .GroupBy(s => s.TenantId)
            .ToDictionary(g => g.Key, g => g.First().Limit);

        var userIds = assignments.Select(a => a.UserId).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.Role })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userMap = users.ToDictionary(u => u.Id);

        var rows = new List<CriticalUserDto>();
        foreach (var row in assignments)
        {
            var limit = capsByTenant.GetValueOrDefault(row.TenantId, TenantLimits.DefaultMaxActiveRegistersPerUser);
            var status = LimitDashboardMapper.ClassifyUser(limit, row.Count);
            if (status is null)
                continue;

            names.TryGetValue(row.TenantId, out var label);
            userMap.TryGetValue(row.UserId, out var user);
            var userName = user?.UserName ?? row.UserId;
            var displayName = string.IsNullOrWhiteSpace($"{user?.FirstName} {user?.LastName}".Trim())
                ? userName
                : $"{user!.FirstName} {user.LastName}".Trim();

            rows.Add(new CriticalUserDto
            {
                TenantId = row.TenantId,
                TenantName = label.Name,
                TenantSlug = label.Slug,
                UserId = row.UserId,
                UserName = userName,
                DisplayName = displayName,
                Role = user?.Role ?? string.Empty,
                LimitKey = TenantLimitKeys.MaxActiveRegistersPerUser,
                Limit = LimitDashboardMapper.ToInt(limit),
                Current = row.Count,
                Percentage = (double)LimitDashboardMapper.ComputePercent(limit, row.Count),
                Status = status,
                RecommendedAction = LimitDashboardMapper.RecommendedAction(
                    TenantLimitKeys.MaxActiveRegistersPerUser,
                    status),
            });
        }

        return rows
            .OrderByDescending(r => r.Percentage)
            .ThenBy(r => r.DisplayName)
            .ToList();
    }

    private async Task<IReadOnlyList<LimitActivityDto>> LoadRecentActivityAsync(
        IReadOnlyList<Guid> tenantIds,
        IReadOnlyDictionary<Guid, (string Name, string Slug)> names,
        string? readerUserId,
        CancellationToken cancellationToken)
    {
        var events = await _db.ActivityEvents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => tenantIds.Contains(e.TenantId)
                        && (e.Type == ActivityEventType.LimitApproaching
                            || e.Type == ActivityEventType.LimitExceeded))
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(RecentLogLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid>? readIds = null;
        if (!string.IsNullOrWhiteSpace(readerUserId) && events.Count > 0)
        {
            var ids = events.Select(e => e.Id).ToList();
            readIds = (await _db.ActivityEventReads
                    .AsNoTracking()
                    .Where(r => r.UserId == readerUserId && ids.Contains(r.ActivityEventId))
                    .Select(r => r.ActivityEventId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                .ToHashSet();
        }

        return events.Select(e =>
        {
            names.TryGetValue(e.TenantId, out var label);
            return new LimitActivityDto
            {
                Id = e.Id,
                TimestampUtc = e.CreatedAtUtc,
                TenantId = e.TenantId,
                TenantName = label.Name,
                TenantSlug = label.Slug,
                LimitKey = e.EntityId ?? string.Empty,
                EventType = e.Type.ToString(),
                Status = LimitDashboardMapper.ActivityStatus(e.Type),
                Description = string.IsNullOrWhiteSpace(e.Description) ? e.Title : e.Description!,
                UserName = e.ActorName,
                IsRead = readIds != null && readIds.Contains(e.Id),
            };
        }).ToList();
    }

    private async Task<int> CountUnreadAlertsAsync(
        IReadOnlyList<Guid> tenantIds,
        string? readerUserId,
        CancellationToken cancellationToken)
    {
        var query = _db.ActivityEvents
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(e => tenantIds.Contains(e.TenantId)
                        && (e.Type == ActivityEventType.LimitApproaching
                            || e.Type == ActivityEventType.LimitExceeded));

        if (string.IsNullOrWhiteSpace(readerUserId))
            return await query.CountAsync(cancellationToken).ConfigureAwait(false);

        return await query
            .CountAsync(
                e => !_db.ActivityEventReads.Any(r => r.ActivityEventId == e.Id && r.UserId == readerUserId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> LoadMaxTicketTodayAsync(
        IReadOnlyList<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var rows = await (
                from p in _db.PaymentDetails.IgnoreQueryFilters().AsNoTracking()
                join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on p.CashRegisterId equals r.Id
                where tenantIds.Contains(r.TenantId) && p.CreatedAt >= start && p.CreatedAt < end
                group p by r.TenantId into g
                select new { TenantId = g.Key, Max = g.Max(x => x.TotalAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ToDictionary(x => x.TenantId, x => x.Max);
    }

    private async Task<Dictionary<Guid, Dictionary<string, int>>> LoadChangeCountsAsync(
        IReadOnlyList<Guid> tenantIds,
        CancellationToken cancellationToken)
    {
        var todayStart = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var weekStart = todayStart.AddDays(-7);
        var result = tenantIds.ToDictionary(id => id, _ => new Dictionary<string, int>(StringComparer.Ordinal));

        void Add(Guid tenantId, string key, int value)
        {
            if (!result.TryGetValue(tenantId, out var map))
                return;
            map[key] = value;
        }

        var productAdds = await _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId) && p.CreatedAt >= weekStart)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var productRemoves = await _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId) && !p.IsActive && p.UpdatedAt != null && p.UpdatedAt >= weekStart)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var productNet = productAdds.ToDictionary(x => x.TenantId, x => x.Count);
        foreach (var row in productRemoves)
            productNet[row.TenantId] = productNet.GetValueOrDefault(row.TenantId) - row.Count;
        foreach (var (tenantId, count) in productNet)
            Add(tenantId, TenantLimitKeys.MaxProductsPerTenant, count);

        var userAdds = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId) && m.CreatedAtUtc >= weekStart)
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userRemoves = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId) && !m.IsActive && m.UpdatedAtUtc != null && m.UpdatedAtUtc >= weekStart)
            .GroupBy(m => m.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var userNet = userAdds.ToDictionary(x => x.TenantId, x => x.Count);
        foreach (var row in userRemoves)
            userNet[row.TenantId] = userNet.GetValueOrDefault(row.TenantId) - row.Count;
        foreach (var (tenantId, count) in userNet)
            Add(tenantId, TenantLimitKeys.MaxUsersPerTenant, count);

        var registerChanges = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId)
                        && r.AssignedUserId != null
                        && r.Status != RegisterStatus.Decommissioned
                        && ((r.UpdatedAt != null && r.UpdatedAt >= weekStart) || r.CreatedAt >= weekStart))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in registerChanges)
            Add(row.TenantId, TenantLimitKeys.MaxActiveRegistersPerUser, row.Count);

        var backupAdds = await _db.BackupRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId != null
                        && tenantIds.Contains(r.TenantId.Value)
                        && r.Strategy == BackupStrategyKind.Tenant
                        && r.Status == BackupRunStatus.Succeeded
                        && r.RequestedAt >= weekStart)
            .GroupBy(r => r.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in backupAdds)
            Add(row.TenantId, TenantLimitKeys.MaxBackupsPerTenant, row.Count);

        var backupMb = await (
                from a in _db.BackupArtifacts.IgnoreQueryFilters().AsNoTracking()
                join r in _db.BackupRuns.IgnoreQueryFilters().AsNoTracking() on a.BackupRunId equals r.Id
                where r.TenantId != null
                      && tenantIds.Contains(r.TenantId.Value)
                      && r.Strategy == BackupStrategyKind.Tenant
                      && r.Status == BackupRunStatus.Succeeded
                      && r.RequestedAt >= weekStart
                      && a.ArtifactType == BackupArtifactType.LogicalDump
                group a by r.TenantId!.Value into g
                select new { TenantId = g.Key, Bytes = g.Sum(x => x.ByteSize ?? 0L) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in backupMb)
            Add(row.TenantId, TenantLimitKeys.MaxBackupSizeMb, (int)Math.Round(row.Bytes / (1024m * 1024m), MidpointRounding.AwayFromZero));

        var offlineAdds = await (
                from o in _db.OfflineTransactions.IgnoreQueryFilters().AsNoTracking()
                join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on o.CashRegisterId equals r.Id
                where tenantIds.Contains(r.TenantId) && o.OfflineCreatedAtUtc >= weekStart
                group o by r.TenantId into g
                select new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in offlineAdds)
            Add(row.TenantId, TenantLimitKeys.MaxOfflineTransactions, row.Count);

        var todayPayments = await (
                from p in _db.PaymentDetails.IgnoreQueryFilters().AsNoTracking()
                join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on p.CashRegisterId equals r.Id
                where tenantIds.Contains(r.TenantId) && p.CreatedAt >= todayStart
                group p by r.TenantId into g
                select new
                {
                    TenantId = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(x => x.TotalAmount),
                    Max = g.Max(x => x.TotalAmount),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var priorPayments = await (
                from p in _db.PaymentDetails.IgnoreQueryFilters().AsNoTracking()
                join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on p.CashRegisterId equals r.Id
                where tenantIds.Contains(r.TenantId) && p.CreatedAt >= weekStart && p.CreatedAt < todayStart
                group p by r.TenantId into g
                select new
                {
                    TenantId = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(x => x.TotalAmount),
                    Max = g.Max(x => x.TotalAmount),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var priorByTenant = priorPayments.ToDictionary(x => x.TenantId);
        foreach (var today in todayPayments)
        {
            priorByTenant.TryGetValue(today.TenantId, out var prior);
            var priorCount = prior?.Count ?? 0;
            var priorRevenue = prior?.Revenue ?? 0m;
            var priorMax = prior?.Max ?? 0m;
            Add(
                today.TenantId,
                TenantLimitKeys.DailyMaxTransactions,
                LimitDashboardMapper.DeltaVsAverage(today.Count, priorCount, previousDays: 6));
            Add(
                today.TenantId,
                TenantLimitKeys.DailyMaxRevenue,
                LimitDashboardMapper.DeltaVsAverage(
                    LimitDashboardMapper.ToInt(today.Revenue),
                    LimitDashboardMapper.ToInt(priorRevenue),
                    previousDays: 6));
            Add(
                today.TenantId,
                TenantLimitKeys.MaxTransactionAmount,
                LimitDashboardMapper.ToInt(today.Max - priorMax));
        }

        return result;
    }
}
