using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Aggregates mandant license renewal funnel steps from billing_audit_log + audit_logs.
/// </summary>
public sealed class LicenseRenewalFunnelService : ILicenseRenewalFunnelService
{
    public const int DefaultLookbackDays = 90;
    public const int MaxLookbackDays = 366;

    private readonly AppDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<LicenseRenewalFunnelService> _logger;

    public LicenseRenewalFunnelService(
        AppDbContext db,
        IAuditLogService auditLogService,
        ILogger<LicenseRenewalFunnelService> logger)
    {
        _db = db;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<LicenseRenewalFunnelDto> GetFunnelAsync(
        LicenseRenewalFunnelQuery query,
        CancellationToken cancellationToken = default)
    {
        var toUtc = NormalizeUtc(query.ToUtc) ?? DateTime.UtcNow;
        var fromUtc = NormalizeUtc(query.FromUtc)
            ?? toUtc.AddDays(-DefaultLookbackDays);

        if (fromUtc > toUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        if ((toUtc - fromUtc).TotalDays > MaxLookbackDays)
            fromUtc = toUtc.AddDays(-MaxLookbackDays);

        var reminderTenants = await DistinctBillingTenantsAsync(
                BillingAuditEventTypes.LicenseReminderSent,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var pageViewTenants = await DistinctAuditTenantsAsync(
                AuditEventType.LicenseRenewalPageViewed,
                AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var renewedFromAudit = await DistinctAuditTenantsAsync(
                AuditEventType.LicenseRenewed,
                AuditLogActions.LICENSE_RENEWED,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var extendedFromAudit = await DistinctAuditTenantsAsync(
                AuditEventType.LicenseExtended,
                AuditLogActions.LICENSE_EXTENDED,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var extendedFromBilling = await DistinctBillingTenantsAsync(
                BillingAuditEventTypes.LicenseExtended,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var activatedTenants = await DistinctBillingTenantsAsync(
                BillingAuditEventTypes.LicenseActivated,
                fromUtc,
                toUtc,
                cancellationToken)
            .ConfigureAwait(false);

        var renewed = renewedFromAudit
            .Union(extendedFromAudit)
            .Union(extendedFromBilling)
            .ToHashSet();

        var reminderSent = reminderTenants.Count;
        var total = reminderSent;
        var pageViewed = pageViewTenants.Count;
        var renewedCount = renewed.Count;
        var activated = activatedTenants.Count;

        var conversionRate = total > 0
            ? Math.Round(100.0 * activated / total, 1)
            : 0d;

        return new LicenseRenewalFunnelDto(
            Total: total,
            ReminderSent: reminderSent,
            PageViewed: pageViewed,
            Renewed: renewedCount,
            Activated: activated,
            ConversionRate: conversionRate,
            FromUtc: fromUtc,
            ToUtc: toUtc);
    }

    public async Task<bool> RecordPageViewAsync(
        Guid tenantId,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(actorUserId))
            return false;

        var dayStart = DateTime.UtcNow.Date;
        var dayEnd = dayStart.AddDays(1);

        var alreadyLogged = await _db.AuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.TenantId == tenantId
                    && a.Timestamp >= dayStart
                    && a.Timestamp < dayEnd
                    && (a.ActionType == AuditEventType.LicenseRenewalPageViewed
                        || a.Action == AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED),
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyLogged)
            return false;

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                    AuditLogActions.LICENSE_RENEWAL_PAGE_VIEWED,
                    AuditLogEntityTypes.SYSTEM_CONFIG,
                    actorUserId,
                    string.IsNullOrWhiteSpace(actorRole) ? "Manager" : actorRole,
                    description: "License renewal page or modal viewed.",
                    requestData: new { Source = "fa_renewal_ui" },
                    actionType: AuditEventType.LicenseRenewalPageViewed,
                    entityId: tenantId,
                    tenantId: tenantId)
                .ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to record license renewal page view TenantId={TenantId}",
                tenantId);
            return false;
        }
    }

    private async Task<HashSet<Guid>> DistinctBillingTenantsAsync(
        string action,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var ids = await _db.BillingAuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(l => l.Action == action
                && l.TenantId != null
                && l.TimestampUtc >= fromUtc
                && l.TimestampUtc <= toUtc)
            .Select(l => l.TenantId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.ToHashSet();
    }

    private async Task<HashSet<Guid>> DistinctAuditTenantsAsync(
        AuditEventType actionType,
        string action,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var ids = await _db.AuditLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId != Guid.Empty
                && a.Timestamp >= fromUtc
                && a.Timestamp <= toUtc
                && (a.ActionType == actionType || a.Action == action))
            .Select(a => a.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.ToHashSet();
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;
        var v = value.Value;
        return v.Kind switch
        {
            DateTimeKind.Utc => v,
            DateTimeKind.Local => v.ToUniversalTime(),
            _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
        };
    }
}
