using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Super Admin read model merging <c>billing_audit_log</c> license/sale events with fiscal
/// <c>audit_logs</c> LICENSE_* rows. No new write path.
/// </summary>
public sealed class LicenseAuditQueryService : ILicenseAuditQueryService
{
    private const int MaxPageSize = 100;
    private const int SourceFetchCap = 500;

    private static readonly string[] BillingLicenseActions =
    [
        BillingAuditEventTypes.SaleCreated,
        BillingAuditEventTypes.SaleCancelled,
        BillingAuditEventTypes.SaleRefunded,
        BillingAuditEventTypes.LicenseActivated,
        BillingAuditEventTypes.LicenseExtended,
        BillingAuditEventTypes.LicenseReminderSent,
    ];

    private readonly AppDbContext _db;
    private readonly int _gracePeriodDays;

    public LicenseAuditQueryService(AppDbContext db, IOptions<LicenseOptions> licenseOptions)
    {
        _db = db;
        _gracePeriodDays = Math.Max(1, licenseOptions.Value.GracePeriodDays > 0
            ? licenseOptions.Value.GracePeriodDays
            : LicenseGracePeriodConfig.GracePeriodDays);
    }

    public async Task<LicenseAuditLogListResponse> ListAsync(
        LicenseAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);

        var billing = await LoadBillingCandidatesAsync(query, cancellationToken).ConfigureAwait(false);
        var audit = await LoadAuditCandidatesAsync(query, cancellationToken).ConfigureAwait(false);

        var merged = LicenseAuditLogMapper.DeduplicatePreferBilling(billing.Concat(audit));

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var actionFilter = query.Action.Trim();
            merged = merged
                .Where(r => string.Equals(r.Action, actionFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = merged.OrderByDescending(r => r.CreatedAtUtc).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(LicenseAuditLogMapper.ToDto)
            .ToList();

        return new LicenseAuditLogListResponse(items, page, pageSize, totalCount);
    }

    private async Task<List<LicenseAuditLogMapper.Candidate>> LoadBillingCandidatesAsync(
        LicenseAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var q = _db.BillingAuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(l => l.Tenant)
            .Where(l => BillingLicenseActions.Contains(l.Action));

        if (query.TenantId.HasValue)
            q = q.Where(l => l.TenantId == query.TenantId.Value);

        if (query.FromUtc.HasValue)
            q = q.Where(l => l.TimestampUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            q = q.Where(l => l.TimestampUtc <= query.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            q = q.Where(l => l.Action == action);
        }

        var rows = await q
            .OrderByDescending(l => l.TimestampUtc)
            .Take(SourceFetchCap)
            .Select(l => new
            {
                l.Id,
                l.TimestampUtc,
                l.TenantId,
                TenantName = l.Tenant != null ? l.Tenant.Name : null,
                l.Action,
                l.Details,
                l.UserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return [];

        var names = await LoadUserDisplayNamesAsync(
                rows.Select(r => r.UserId.ToString("D")),
                cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r =>
            {
                names.TryGetValue(r.UserId.ToString("D"), out var display);
                var performedBy = r.UserId == Guid.Empty
                    ? "System"
                    : string.IsNullOrWhiteSpace(display) ? r.UserId.ToString("D") : display;
                return LicenseAuditLogMapper.FromBillingRow(
                    r.Id,
                    r.TimestampUtc,
                    r.TenantId,
                    r.TenantName,
                    r.Action,
                    r.Details,
                    performedBy);
            })
            .ToList();
    }

    private async Task<List<LicenseAuditLogMapper.Candidate>> LoadAuditCandidatesAsync(
        LicenseAuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var q = _db.AuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a =>
                a.ActionType == AuditEventType.LicenseRenewed
                || a.ActionType == AuditEventType.LicenseExtended
                || a.ActionType == AuditEventType.LicenseUpdated
                || a.ActionType == AuditEventType.LicenseRenewalPageViewed
                || a.Action == AuditLogActions.LICENSE_RENEWED
                || a.Action == AuditLogActions.LICENSE_EXTENDED
                || a.Action == AuditLogActions.LICENSE_UPDATED
                || a.Action == AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED);

        if (query.TenantId.HasValue)
            q = q.Where(a => a.TenantId == query.TenantId.Value);

        if (query.FromUtc.HasValue)
            q = q.Where(a => a.Timestamp >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            q = q.Where(a => a.Timestamp <= query.ToUtc.Value);

        var rows = await q
            .OrderByDescending(a => a.Timestamp)
            .Take(SourceFetchCap)
            .Select(a => new
            {
                a.Id,
                a.Timestamp,
                a.TenantId,
                a.ActionType,
                a.Action,
                a.Description,
                a.RequestData,
                a.UserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return [];

        var tenantIds = rows
            .Select(r => r.TenantId)
            .Distinct()
            .ToList();

        var tenantNames = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken)
            .ConfigureAwait(false);

        var names = await LoadUserDisplayNamesAsync(
                rows.Select(r => r.UserId).Where(id => !string.IsNullOrWhiteSpace(id))!,
                cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r =>
            {
                tenantNames.TryGetValue(r.TenantId, out var tenantName);

                string? performedBy = null;
                if (!string.IsNullOrWhiteSpace(r.UserId))
                {
                    names.TryGetValue(r.UserId, out performedBy);
                    performedBy ??= r.UserId;
                }

                return LicenseAuditLogMapper.FromAuditLogRow(
                    r.Id,
                    r.Timestamp,
                    r.TenantId,
                    tenantName,
                    r.ActionType,
                    r.Action,
                    r.Description,
                    r.RequestData,
                    performedBy,
                    _gracePeriodDays);
            })
            .ToList();
    }

    private async Task<Dictionary<string, string>> LoadUserDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.UserName })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var u in users)
        {
            var display = $"{u.FirstName} {u.LastName}".Trim();
            map[u.Id] = string.IsNullOrWhiteSpace(display)
                ? (u.UserName ?? u.Id)
                : display;
        }

        return map;
    }
}
