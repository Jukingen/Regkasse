using System.Net;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Trial;

public sealed class TrialService : ITrialService
{
    private readonly AppDbContext _db;
    private readonly IOptionsMonitor<TrialOptions> _options;
    private readonly IEmailService _email;
    private readonly IActivityEventService _activity;
    private readonly ITenantService _tenantService;
    private readonly ITrialConversionService _conversion;
    private readonly ILogger<TrialService> _logger;

    public TrialService(
        AppDbContext db,
        IOptionsMonitor<TrialOptions> options,
        IEmailService email,
        IActivityEventService activity,
        ITenantService tenantService,
        ITrialConversionService conversion,
        ILogger<TrialService> logger)
    {
        _db = db;
        _options = options;
        _email = email;
        _activity = activity;
        _tenantService = tenantService;
        _conversion = conversion;
        _logger = logger;
    }

    public int ResolveDurationDays(int? requestedDays)
    {
        var opt = _options.CurrentValue;
        var allowed = opt.AllowedDurationDays is { Length: > 0 }
            ? opt.AllowedDurationDays
            : [14, 30, 60, 90];
        var fallback = opt.DefaultDurationDays > 0 ? opt.DefaultDurationDays : 14;
        if (!requestedDays.HasValue || requestedDays.Value <= 0)
            return fallback;
        if (allowed.Contains(requestedDays.Value))
            return requestedDays.Value;
        return fallback;
    }

    public void ApplyTrialGrant(Tenant tenant, int durationDays, DateTime nowUtc)
    {
        var days = durationDays > 0 ? durationDays : ResolveDurationDays(null);
        var ends = nowUtc.AddDays(days);
        tenant.TrialStartedAtUtc = nowUtc;
        tenant.TrialEndsAtUtc = ends;
        tenant.TrialStatus = TrialStatuses.Active;
        tenant.TrialReminder7dSent = false;
        tenant.TrialReminder3dSent = false;
        tenant.TrialReminder1dSent = false;
        tenant.TrialConvertedAtUtc = null;
        tenant.TrialDeletedAtUtc = null;
        tenant.TrialGracePeriodEndsAtUtc = null;
        tenant.LicenseKey = null;
        tenant.LicenseValidUntilUtc = ends;
        tenant.UpdatedAt = nowUtc;
    }

    public async Task<TrialDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TrialStatus != null)
            .OrderBy(t => t.TrialEndsAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summaries = rows.Select(t => ToSummary(t, now)).ToList();
        var active = summaries.Where(s => string.Equals(s.TrialStatus, TrialStatuses.Active, StringComparison.OrdinalIgnoreCase)).ToList();
        var expired = summaries.Where(s => string.Equals(s.TrialStatus, TrialStatuses.Expired, StringComparison.OrdinalIgnoreCase)).ToList();
        var converted = summaries.Count(s => string.Equals(s.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase));
        var deleted = summaries.Count(s => string.Equals(s.TrialStatus, TrialStatuses.Deleted, StringComparison.OrdinalIgnoreCase));
        var expiringSoon = active.Where(s => s.DaysRemaining is >= 0 and <= 7).ToList();
        var closed = converted + deleted;
        var conversionDenom = closed + active.Count + expired.Count;
        var rate = conversionDenom == 0 ? 0d : Math.Round(100d * converted / conversionDenom, 1);

        return new TrialDashboardDto(
            active.Count,
            expiringSoon.Count,
            expired.Count,
            converted,
            deleted,
            rate,
            active,
            expiringSoon,
            expired);
    }

    public async Task<TrialTenantSummaryDto?> GetTenantTrialAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        return tenant == null ? null : ToSummary(tenant, DateTime.UtcNow);
    }

    public async Task<(TrialTenantSummaryDto? Result, string? Error)> GrantOrRestartTrialAsync(
        Guid tenantId,
        int? durationDays,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.Enabled)
            return (null, "Trial management is disabled.");

        var tenant = await LoadMutableAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (TenantStatuses.IsRemoved(tenant.Status))
            return (null, "Deleted tenants cannot receive a trial.");

        var now = DateTime.UtcNow;
        ApplyTrialGrant(tenant, ResolveDurationDays(durationDays), now);
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryPublishAsync(
            tenant.Id,
            ActivityEventType.TrialStarted,
            "Trial started",
            $"Trial until {tenant.TrialEndsAtUtc:yyyy-MM-dd} UTC",
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return (ToSummary(tenant, now), null);
    }

    public async Task<(TrialTenantSummaryDto? Result, string? Error)> ExtendTrialAsync(
        Guid tenantId,
        int additionalDays,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (additionalDays <= 0 || additionalDays > 365)
            return (null, "AdditionalDays must be between 1 and 365.");

        var tenant = await LoadMutableAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (!TrialStatuses.IsOpenTrial(tenant.TrialStatus)
            && !string.Equals(tenant.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase))
        {
            if (tenant.TrialStatus is null && tenant.LicenseValidUntilUtc is null)
            {
                // Allow extending into a new managed trial window.
            }
            else if (!TrialStatuses.IsOpenTrial(tenant.TrialStatus))
            {
                return (null, "Tenant is not on an open trial.");
            }
        }

        if (string.Equals(tenant.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tenant.TrialStatus, TrialStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            return (null, "Converted or deleted trials cannot be extended.");

        var now = DateTime.UtcNow;
        var baseEnd = tenant.TrialEndsAtUtc.HasValue && tenant.TrialEndsAtUtc.Value > now
            ? tenant.TrialEndsAtUtc.Value
            : now;
        var newEnd = baseEnd.AddDays(additionalDays);

        if (tenant.TrialStartedAtUtc is null)
            tenant.TrialStartedAtUtc = now;
        tenant.TrialEndsAtUtc = newEnd;
        tenant.TrialStatus = TrialStatuses.Active;
        tenant.TrialGracePeriodEndsAtUtc = null;
        tenant.TrialReminder7dSent = false;
        tenant.TrialReminder3dSent = false;
        tenant.TrialReminder1dSent = false;
        tenant.LicenseKey = null;
        tenant.LicenseValidUntilUtc = newEnd;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await TryPublishAsync(
            tenant.Id,
            ActivityEventType.TrialExtended,
            "Trial extended",
            $"Extended by {additionalDays} day(s); ends {newEnd:yyyy-MM-dd} UTC",
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return (ToSummary(tenant, now), null);
    }

    public async Task<(TrialTenantSummaryDto? Result, string? Error)> ConvertToPaidAsync(
        Guid tenantId,
        Guid licenseSaleId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var (conversion, error) = await _conversion
            .ConvertToPaidAsync(
                tenantId,
                licenseSaleId,
                addRemainingTrialDays: true,
                notes: null,
                actorUserId,
                actorRole: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (error != null)
            return (null, error);
        if (conversion == null || !conversion.Success)
            return (null, conversion?.Error ?? "Conversion failed.");

        return (await GetTenantTrialAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<TrialAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var since = now.AddDays(-30);
        var rows = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TrialStatus != null)
            .Select(t => new
            {
                t.TrialStatus,
                t.TrialStartedAtUtc,
                t.TrialEndsAtUtc,
                t.TrialConvertedAtUtc,
                t.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var created30 = rows.Count(r =>
            (r.TrialStartedAtUtc ?? r.CreatedAt) >= since);
        var active = rows.Count(r => string.Equals(r.TrialStatus, TrialStatuses.Active, StringComparison.OrdinalIgnoreCase));
        var expired = rows.Count(r => string.Equals(r.TrialStatus, TrialStatuses.Expired, StringComparison.OrdinalIgnoreCase));
        var converted = rows.Count(r => string.Equals(r.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase));
        var deleted = rows.Count(r => string.Equals(r.TrialStatus, TrialStatuses.Deleted, StringComparison.OrdinalIgnoreCase));
        var denom = converted + active + expired;
        var rate = denom == 0 ? 0d : Math.Round(100d * converted / denom, 1);

        var convertPairs = rows
            .Where(r => string.Equals(r.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase)
                        && r.TrialStartedAtUtc.HasValue
                        && r.TrialConvertedAtUtc.HasValue)
            .Select(r => (r.TrialConvertedAtUtc!.Value - r.TrialStartedAtUtc!.Value).TotalDays)
            .ToList();
        double? avgDays = convertPairs.Count == 0
            ? null
            : Math.Round(convertPairs.Average(), 1);

        var sales = await _db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.ConvertedFromTrial)
            .Select(s => s.LicensePlan)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var mostCommon = sales
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var byPlan = sales
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => new TrialPlanConversionBucketDto(g.Key, g.Count()))
            .ToList();

        var allowed = _options.CurrentValue.AllowedDurationDays is { Length: > 0 }
            ? _options.CurrentValue.AllowedDurationDays
            : [14, 30, 60, 90];
        var buckets = new List<TrialDurationConversionBucketDto>();
        foreach (var days in allowed.Distinct().OrderBy(d => d))
        {
            var started = rows.Count(r =>
                r.TrialStartedAtUtc.HasValue
                && r.TrialEndsAtUtc.HasValue
                && Math.Abs(
                    (r.TrialEndsAtUtc.Value - r.TrialStartedAtUtc.Value).TotalDays - days) < 0.6);
            var convertedBucket = rows.Count(r =>
                string.Equals(r.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase)
                && r.TrialStartedAtUtc.HasValue
                && r.TrialEndsAtUtc.HasValue
                && Math.Abs(
                    (r.TrialEndsAtUtc.Value - r.TrialStartedAtUtc.Value).TotalDays - days) < 0.6);
            buckets.Add(new TrialDurationConversionBucketDto(days, convertedBucket, started));
        }

        var since90 = now.AddDays(-90);
        var monthly = rows
            .Where(r => (r.TrialStartedAtUtc ?? r.CreatedAt) >= since90)
            .GroupBy(r =>
            {
                var stamp = r.TrialStartedAtUtc ?? r.CreatedAt;
                return $"{stamp.Year:D4}-{stamp.Month:D2}";
            })
            .OrderBy(g => g.Key)
            .Select(g => new TrialMonthlyTrendDto(
                g.Key,
                g.Count(),
                g.Count(x => string.Equals(x.TrialStatus, TrialStatuses.Converted, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        return new TrialAnalyticsDto(
            created30,
            active,
            expired,
            converted,
            deleted,
            rate,
            avgDays,
            mostCommon,
            buckets,
            byPlan,
            monthly);
    }

    public async Task<(bool Success, string? Error)> SoftDeleteTrialAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadMutableAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (false, "Tenant not found.");

        var (ok, error) = await _tenantService
            .SoftDeleteAsync(tenantId, actorUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return (false, error);

        // Reload after soft-delete (same context may still track the entity).
        tenant = await LoadMutableAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant != null)
        {
            tenant.TrialStatus = TrialStatuses.Deleted;
            tenant.TrialDeletedAtUtc = DateTime.UtcNow;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = actorUserId;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await TryPublishAsync(
            tenantId,
            ActivityEventType.TrialDeleted,
            "Trial tenant soft-deleted",
            "Expired/cancelled trial soft-archived (RKSV data retained).",
            actorUserId,
            cancellationToken).ConfigureAwait(false);

        return (true, null);
    }

    public async Task<int> ProcessExpiryAndGraceAsync(CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return 0;

        var now = DateTime.UtcNow;
        var graceDays = opt.GracePeriodDays > 0 ? opt.GracePeriodDays : 7;
        var candidates = await _db.Tenants
            .Where(t => t.TrialStatus == TrialStatuses.Active
                        && t.TrialEndsAtUtc != null
                        && t.TrialEndsAtUtc <= now)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var count = 0;
        foreach (var tenant in candidates)
        {
            tenant.TrialStatus = TrialStatuses.Expired;
            tenant.TrialGracePeriodEndsAtUtc = now.AddDays(graceDays);
            tenant.UpdatedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await TryPublishAsync(
                tenant.Id,
                ActivityEventType.TrialExpired,
                "Trial expired",
                $"Grace until {tenant.TrialGracePeriodEndsAtUtc:yyyy-MM-dd} UTC",
                null,
                cancellationToken).ConfigureAwait(false);

            var to = tenant.Email;
            if (!string.IsNullOrWhiteSpace(to))
            {
                await _email.TrySendHtmlAsync(
                    to,
                    "Your Regkasse trial has expired",
                    BuildExpiredBody(tenant.Name, tenant.TrialGracePeriodEndsAtUtc),
                    cancellationToken).ConfigureAwait(false);
            }

            count++;
        }

        return count;
    }

    public async Task<int> ProcessRemindersAsync(CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return 0;

        var now = DateTime.UtcNow;
        var anchors = (opt.ReminderDays is { Length: > 0 } ? opt.ReminderDays : [7, 3, 1])
            .Distinct()
            .ToArray();

        var active = await _db.Tenants
            .Where(t => t.TrialStatus == TrialStatuses.Active && t.TrialEndsAtUtc != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        foreach (var tenant in active)
        {
            var ends = DateTime.SpecifyKind(tenant.TrialEndsAtUtc!.Value, DateTimeKind.Utc);
            var daysRemaining = (int)Math.Ceiling((ends - now).TotalDays);
            if (!anchors.Contains(daysRemaining))
                continue;

            var alreadySent = daysRemaining switch
            {
                7 => tenant.TrialReminder7dSent,
                3 => tenant.TrialReminder3dSent,
                1 => tenant.TrialReminder1dSent,
                _ => false,
            };
            if (alreadySent)
                continue;

            var to = tenant.Email;
            if (string.IsNullOrWhiteSpace(to))
            {
                MarkReminderSent(tenant, daysRemaining);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var (subject, body) = BuildReminder(daysRemaining, tenant.Name, ends);
            var ok = await _email.TrySendHtmlAsync(to, subject, body, cancellationToken).ConfigureAwait(false);
            MarkReminderSent(tenant, daysRemaining);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await TryPublishAsync(
                tenant.Id,
                ActivityEventType.TrialExpiringSoon,
                $"Trial reminder ({daysRemaining}d)",
                ok ? $"Reminder emailed to {to}" : $"Reminder flagged (email skipped) for {to}",
                null,
                cancellationToken,
                dedupKey: $"trial-reminder:{tenant.Id:N}:{daysRemaining}:{ends:yyyyMMdd}").ConfigureAwait(false);

            sent++;
        }

        return sent;
    }

    public async Task<int> ProcessCleanupAsync(CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return 0;

        var now = DateTime.UtcNow;
        var afterGrace = opt.AutoDeleteAfterGraceDays > 0 ? opt.AutoDeleteAfterGraceDays : 30;
        // Grace ended, then wait AutoDeleteAfterGraceDays.
        var cutoff = now.AddDays(-afterGrace);

        var candidates = await _db.Tenants
            .Where(t => t.TrialStatus == TrialStatuses.Expired
                        && t.TrialGracePeriodEndsAtUtc != null
                        && t.TrialGracePeriodEndsAtUtc < cutoff
                        && t.TrialDeletedAtUtc == null
                        && t.Status != TenantStatuses.Cancelled
                        && t.Status != TenantStatuses.Archived
                        && t.Status != TenantStatuses.Deleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var count = 0;
        foreach (var tenantId in candidates)
        {
            var (ok, error) = await SoftDeleteTrialAsync(tenantId, actorUserId: "system:trial-cleanup", cancellationToken)
                .ConfigureAwait(false);
            if (!ok)
            {
                _logger.LogWarning(
                    "Trial cleanup soft-delete failed for {TenantId}: {Error}",
                    tenantId,
                    error);
                continue;
            }

            count++;
        }

        if (count > 0)
        {
            _logger.LogInformation("Trial cleanup soft-archived {Count} expired trial tenant(s).", count);
        }

        return count;
    }

    private async Task<Tenant?> LoadMutableAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (SystemTenantIds.IsPlatformTenantId(tenantId))
            return null;

        return await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);
    }

    private static TrialTenantSummaryDto ToSummary(Tenant t, DateTime nowUtc)
    {
        int? days = null;
        if (t.TrialEndsAtUtc.HasValue)
        {
            var ends = DateTime.SpecifyKind(t.TrialEndsAtUtc.Value, DateTimeKind.Utc);
            days = (int)Math.Ceiling((ends - nowUtc).TotalDays);
        }

        return new TrialTenantSummaryDto(
            t.Id,
            t.Name,
            t.Slug,
            t.Email,
            t.TrialStatus,
            t.TrialStartedAtUtc,
            t.TrialEndsAtUtc,
            t.TrialGracePeriodEndsAtUtc,
            t.TrialConvertedAtUtc,
            t.TrialDeletedAtUtc,
            days,
            t.TrialReminder7dSent,
            t.TrialReminder3dSent,
            t.TrialReminder1dSent);
    }

    private static void MarkReminderSent(Tenant tenant, int daysRemaining)
    {
        switch (daysRemaining)
        {
            case 7:
                tenant.TrialReminder7dSent = true;
                break;
            case 3:
                tenant.TrialReminder3dSent = true;
                break;
            case 1:
                tenant.TrialReminder1dSent = true;
                break;
        }

        tenant.UpdatedAt = DateTime.UtcNow;
    }

    private static (string Subject, string Body) BuildReminder(int days, string tenantName, DateTime endsUtc)
    {
        var name = WebUtility.HtmlEncode(tenantName);
        return days switch
        {
            7 => (
                "Your Regkasse trial ends in 7 days",
                $"<p>Your trial for <strong>{name}</strong> ends on {endsUtc:yyyy-MM-dd} UTC (7 days).</p><p>Upgrade now to keep full access.</p>"),
            3 => (
                "Your Regkasse trial ends in 3 days",
                $"<p>Your trial for <strong>{name}</strong> ends on {endsUtc:yyyy-MM-dd} UTC (3 days).</p><p>Upgrade now to keep full access.</p>"),
            1 => (
                "Your Regkasse trial ends tomorrow!",
                $"<p>Your trial for <strong>{name}</strong> ends tomorrow ({endsUtc:yyyy-MM-dd} UTC).</p><p>Upgrade now to avoid interruption.</p>"),
            _ => (
                $"Your Regkasse trial ends in {days} day(s)",
                $"<p>Your trial for <strong>{name}</strong> ends on {endsUtc:yyyy-MM-dd} UTC.</p>"),
        };
    }

    private static string BuildExpiredBody(string tenantName, DateTime? graceEnds) =>
        $"<p>Your trial for <strong>{WebUtility.HtmlEncode(tenantName)}</strong> has expired.</p>"
        + (graceEnds.HasValue
            ? $"<p>A grace period applies until {graceEnds:yyyy-MM-dd} UTC. Upgrade to restore full access.</p>"
            : "<p>Please upgrade to restore full access.</p>");

    private async Task TryPublishAsync(
        Guid tenantId,
        ActivityEventType type,
        string title,
        string? description,
        string? actorUserId,
        CancellationToken cancellationToken,
        string? dedupKey = null)
    {
        try
        {
            await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    type,
                    title,
                    description,
                    ActorUserId: actorUserId,
                    EntityType: "Tenant",
                    EntityId: tenantId.ToString("D"),
                    DedupKey: dedupKey),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish trial activity {Type} for {TenantId}", type, tenantId);
        }
    }
}
