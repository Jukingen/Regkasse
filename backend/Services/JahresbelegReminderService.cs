using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IJahresbelegReminderService
{
    /// <summary>
    /// On 1 December (Europe/Vienna), publishes one warning per active Austrian tenant
    /// that still has no Jahresbeleg for the current year. Other days publish nothing.
    /// </summary>
    Task<int> CheckAndNotifyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Jahresbeleg status for every operating tenant, or only <paramref name="tenantId"/> when set.
    /// </summary>
    Task<IReadOnlyList<JahresbelegTenantStatusDto>> GetStatusAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class JahresbelegReminderService : IJahresbelegReminderService
{
    public const string StatusCreated = "created";
    public const string StatusMissing = "missing";
    public const string StatusNotDue = "notDue";

    private readonly AppDbContext _db;
    private readonly IActivityEventService _activity;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger<JahresbelegReminderService> _logger;

    public JahresbelegReminderService(
        AppDbContext db,
        IActivityEventService activity,
        ICurrentTenantAccessor tenantAccessor,
        TimeProvider time,
        ILogger<JahresbelegReminderService> logger)
    {
        _db = db;
        _activity = activity;
        _tenantAccessor = tenantAccessor;
        _time = time;
        _logger = logger;
    }

    public async Task<int> CheckAndNotifyAsync(CancellationToken cancellationToken = default)
    {
        var viennaNow = ViennaNow();
        if (!JahresbelegDeadline.IsReminderDay(viennaNow))
            return 0;

        var rows = await LoadRowsAsync(tenantId: null, viennaNow, cancellationToken).ConfigureAwait(false);
        var sent = 0;
        var previous = _tenantAccessor.TenantId;
        try
        {
            foreach (var row in rows)
            {
                if (row.Status != StatusMissing)
                    continue;

                var dedupKey = JahresbelegDeadline.DedupKey(row.TenantId, row.Year);
                var already = await _db.ActivityEvents
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .AnyAsync(e => e.TenantId == row.TenantId && e.DedupKey == dedupKey, cancellationToken)
                    .ConfigureAwait(false);
                if (already)
                    continue;

                _tenantAccessor.TenantId = row.TenantId;
                await _activity.PublishAsync(
                        new ActivityEventPublishRequest(
                            row.TenantId,
                            ActivityEventType.JahresbelegDeadlineReminder,
                            Title: "Jahresbeleg deadline",
                            Description: JahresbelegDeadline.WarningMessage,
                            DedupKey: dedupKey,
                            EntityType: "tenant",
                            EntityId: row.TenantId.ToString("D"),
                            Metadata: new Dictionary<string, object>
                            {
                                ["tenantId"] = row.TenantId.ToString("D"),
                                ["year"] = row.Year,
                                ["deadline"] = row.Deadline.ToString("yyyy-MM-dd"),
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);
                sent++;
            }
        }
        finally
        {
            _tenantAccessor.TenantId = previous;
        }

        if (sent > 0)
        {
            _logger.LogInformation(
                "Jahresbeleg deadline reminders published: {Count} for year {Year}",
                sent,
                JahresbelegDeadline.YearDue(viennaNow));
        }

        return sent;
    }

    public Task<IReadOnlyList<JahresbelegTenantStatusDto>> GetStatusAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        LoadRowsAsync(tenantId, ViennaNow(), cancellationToken);

    private async Task<IReadOnlyList<JahresbelegTenantStatusDto>> LoadRowsAsync(
        Guid? tenantId,
        DateTime viennaNow,
        CancellationToken cancellationToken)
    {
        var yearDue = JahresbelegDeadline.YearDue(viennaNow);
        var deadline = JahresbelegDeadline.Deadline(yearDue);
        var inWindow = JahresbelegDeadline.IsInReminderWindow(viennaNow);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.Status == TenantStatuses.Active
                && t.Id != SystemTenantIds.Platform
                && (tenantId == null || t.Id == tenantId))
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
            return [];

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var countries = await _db.CompanySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId))
            .Select(s => new { s.TenantId, s.Country })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var countryByTenant = countries
            .GroupBy(s => s.TenantId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Country).FirstOrDefault());

        var registers = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r =>
                tenantIds.Contains(r.TenantId)
                && r.IsActive
                && r.Status != RegisterStatus.Decommissioned
                && r.Status != RegisterStatus.Disabled)
            .Select(r => new { r.Id, r.TenantId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var registersByTenant = registers
            .GroupBy(r => r.TenantId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToHashSet());

        var coveredRegisterIds = await _db.PaymentDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p =>
                p.IsActive
                && p.RksvSpecialReceiptYear == yearDue
                && (
                    p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                    || (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                        && p.RksvSpecialReceiptMonth == 12)))
            .Select(p => p.CashRegisterId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var covered = coveredRegisterIds.ToHashSet();

        var rows = new List<JahresbelegTenantStatusDto>();
        foreach (var tenant in tenants.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (countryByTenant.TryGetValue(tenant.Id, out var country)
                && !string.IsNullOrWhiteSpace(country)
                && !string.Equals(country, "AT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!registersByTenant.TryGetValue(tenant.Id, out var registerIds) || registerIds.Count == 0)
                continue;

            var created = registerIds.Any(id => covered.Contains(id));
            var status = created
                ? StatusCreated
                : inWindow ? StatusMissing : StatusNotDue;
            rows.Add(new JahresbelegTenantStatusDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                Year = yearDue,
                Deadline = deadline,
                Status = status,
                Warning = status == StatusMissing ? JahresbelegDeadline.WarningMessage : null,
            });
        }

        return rows;
    }

    private DateTime ViennaNow()
    {
        var utc = _time.GetUtcNow().UtcDateTime;
        return TimeZoneInfo.ConvertTimeFromUtc(utc, PostgreSqlUtcDateTime.AustriaTimeZone);
    }
}
