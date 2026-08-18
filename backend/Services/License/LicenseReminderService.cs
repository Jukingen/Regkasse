using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Sends scheduled mandant license expiry emails at configured calendar-day anchors before expiry
/// (and optionally once when expired), plus grace-period reminders while inside the grace window.
/// </summary>
public sealed class LicenseReminderService : ILicenseReminderService
{
    private static readonly int[] DefaultReminderAnchors = [30, 15, 7, 3, 1];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ILicenseReminderEmailSender _emailSender;
    private readonly IBillingAuditService _billingAudit;
    private readonly IOptions<LicenseOptions> _licenseOptions;
    private readonly IOptions<EmailSmtpOptions> _smtpOptions;
    private readonly ILogger<LicenseReminderService> _logger;

    public LicenseReminderService(
        AppDbContext db,
        ILicenseReminderEmailSender emailSender,
        IBillingAuditService billingAudit,
        IOptions<LicenseOptions> licenseOptions,
        IOptions<EmailSmtpOptions> smtpOptions,
        ILogger<LicenseReminderService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _billingAudit = billingAudit;
        _licenseOptions = licenseOptions;
        _smtpOptions = smtpOptions;
        _logger = logger;
    }

    public async Task<LicenseReminderRunResult> SendDueMandantExpiryRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var anchors = ResolveMandantAnchors();
        var sendExpired = _licenseOptions.Value.SendExpiredReminder;
        if (anchors.Length == 0 && !sendExpired)
            return new LicenseReminderRunResult(0, 0, 0);

        var maxAnchor = anchors.Length > 0 ? anchors.Max() : 0;
        var archiveAfterDays = Math.Max(1, _licenseOptions.Value.ArchiveAfterDays);
        var now = DateTime.UtcNow;
        var expiredCutoff = now.AddDays(-archiveAfterDays);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.Status == TenantStatuses.Active
                && t.LicenseValidUntilUtc != null
                && (
                    (maxAnchor > 0
                     && t.LicenseValidUntilUtc > now
                     && t.LicenseValidUntilUtc <= now.AddDays(maxAnchor + 1))
                    || (sendExpired
                        && t.LicenseValidUntilUtc <= now
                        && t.LicenseValidUntilUtc >= expiredCutoff)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var tenant in tenants)
        {
            var (daysRemaining, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
                tenant.LicenseValidUntilUtc,
                tenant.LicenseKey,
                now);

            var isPreExpiryAnchor =
                kind == "active"
                && daysRemaining is > 0
                && anchors.Contains(daysRemaining.Value);

            var isExpiredDue =
                sendExpired
                && daysRemaining is <= 0
                && daysRemaining >= -archiveAfterDays;

            if (!isPreExpiryAnchor && !isExpiredDue)
            {
                skipped++;
                continue;
            }

            if (isExpiredDue)
            {
                await _db.Tenants
                    .Where(t => t.Id == tenant.Id && t.Status == TenantStatuses.Active)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(t => t.Status, TenantStatuses.Suspended)
                            .SetProperty(t => t.IsActive, false)
                            .SetProperty(t => t.UpdatedAt, now),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var daysBefore = isExpiredDue ? 0 : daysRemaining!.Value;
            var validUntil = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc!.Value, DateTimeKind.Utc);
            var dedupKey = BuildDedupKey(tenant.Id, validUntil, daysBefore);

            if (await WasReminderAlreadySentAsync(tenant.Id, dedupKey, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            var recipients = await ResolveReminderRecipientsAsync(
                    tenant.Id,
                    tenant.Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipients.Count == 0)
            {
                _logger.LogDebug(
                    "Skipping mandant license reminder for tenant {TenantId}: no recipient email.",
                    tenant.Id);
                skipped++;
                continue;
            }

            var renewUrl = _licenseOptions.Value.AdminLicenseUrl;
            var supportEmail = ResolveSupportEmail();
            var anyDelivered = false;
            var failedThisTenant = false;
            string? firstRecipient = null;

            foreach (var recipient in recipients)
            {
                var content = LicenseReminderEmailComposer.Build(
                    LicenseReminderEmailComposer.FromTenant(
                        tenant,
                        daysRemaining,
                        recipient.DisplayName,
                        renewUrl,
                        supportEmail));
                var delivered = await _emailSender
                    .TrySendTenantLicenseReminderAsync(
                        recipient.Email,
                        content.Subject,
                        content.HtmlBody,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (delivered)
                {
                    anyDelivered = true;
                    firstRecipient ??= recipient.Email;
                    sent++;
                    _logger.LogInformation(
                        "Mandant license expiry reminder sent for tenant {TenantId} ({DaysBefore}d) to {RecipientEmail}",
                        tenant.Id,
                        daysBefore,
                        recipient.Email);
                }
                else
                {
                    failedThisTenant = true;
                    failed++;
                }
            }

            if (!anyDelivered)
                continue;

            await LogReminderSentAsync(
                    tenant.Id,
                    dedupKey,
                    daysBefore,
                    validUntil,
                    firstRecipient ?? recipients[0].Email,
                    cancellationToken)
                .ConfigureAwait(false);

            // Dedup is logged even when some recipients failed after a partial success.
            if (failedThisTenant)
            {
                _logger.LogWarning(
                    "Mandant license reminder partially delivered for tenant {TenantId} ({DaysBefore}d).",
                    tenant.Id,
                    daysBefore);
            }
        }

        return new LicenseReminderRunResult(sent, skipped, failed);
    }

    public async Task<LicenseReminderRunResult> SendDueGracePeriodRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_licenseOptions.Value.SendGracePeriodReminders)
            return new LicenseReminderRunResult(0, 0, 0);

        var gracePeriodDays = Math.Max(
            1,
            _licenseOptions.Value.GracePeriodDays > 0
                ? _licenseOptions.Value.GracePeriodDays
                : LicenseGracePeriodConfig.GracePeriodDays);
        var anchors = ResolveGraceAnchors();
        var sendUrgent = _licenseOptions.Value.SendGraceUrgentReminder;
        var urgentDays = Math.Max(0, _licenseOptions.Value.GraceUrgentReminderDays);

        if (anchors.Length == 0 && !sendUrgent)
            return new LicenseReminderRunResult(0, 0, 0);

        var now = DateTime.UtcNow;
        var graceStartedCutoff = now.AddDays(-gracePeriodDays);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.Status == TenantStatuses.Active
                && t.LicenseValidUntilUtc != null
                && t.LicenseValidUntilUtc <= now
                && t.LicenseValidUntilUtc >= graceStartedCutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sent = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var tenant in tenants)
        {
            var validUntil = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc!.Value, DateTimeKind.Utc);
            var graceDaysRemaining = GracePeriodReminderMilestones.ResolveGraceDaysRemaining(
                validUntil,
                now,
                gracePeriodDays);

            if (graceDaysRemaining is null)
            {
                skipped++;
                continue;
            }

            if (!GracePeriodReminderMilestones.ShouldSendReminder(
                    graceDaysRemaining.Value,
                    anchors,
                    sendUrgent,
                    urgentDays))
            {
                skipped++;
                continue;
            }

            var daysRemaining = graceDaysRemaining.Value;
            var lockdownDate = GracePeriodReminderMilestones.ResolveLockdownDateUtc(
                validUntil,
                gracePeriodDays);
            var dedupKey = GracePeriodReminderMilestones.BuildDedupKey(
                tenant.Id,
                validUntil,
                daysRemaining);

            if (await WasReminderAlreadySentAsync(tenant.Id, dedupKey, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            var recipients = await ResolveReminderRecipientsAsync(
                    tenant.Id,
                    tenant.Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipients.Count == 0)
            {
                _logger.LogDebug(
                    "Skipping grace-period reminder for tenant {TenantId}: no recipient email.",
                    tenant.Id);
                skipped++;
                continue;
            }

            var renewUrl = _licenseOptions.Value.AdminLicenseUrl;
            var supportEmail = ResolveSupportEmail();
            var anyDelivered = false;
            var failedThisTenant = false;
            string? firstRecipient = null;

            foreach (var recipient in recipients)
            {
                var content = GracePeriodReminderEmailComposer.Build(
                    GracePeriodReminderEmailComposer.FromTenant(
                        tenant,
                        daysRemaining,
                        lockdownDate,
                        recipient.DisplayName,
                        renewUrl,
                        supportEmail));
                var delivered = await _emailSender
                    .TrySendTenantLicenseReminderAsync(
                        recipient.Email,
                        content.Subject,
                        content.HtmlBody,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (delivered)
                {
                    anyDelivered = true;
                    firstRecipient ??= recipient.Email;
                    sent++;
                    _logger.LogInformation(
                        "Mandant grace-period reminder sent for tenant {TenantId} ({GraceDaysRemaining}d left) to {RecipientEmail}",
                        tenant.Id,
                        daysRemaining,
                        recipient.Email);
                }
                else
                {
                    failedThisTenant = true;
                    failed++;
                }
            }

            if (!anyDelivered)
                continue;

            await LogGraceReminderSentAsync(
                    tenant.Id,
                    dedupKey,
                    daysRemaining,
                    validUntil,
                    lockdownDate,
                    firstRecipient ?? recipients[0].Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (failedThisTenant)
            {
                _logger.LogWarning(
                    "Mandant grace-period reminder partially delivered for tenant {TenantId} ({GraceDaysRemaining}d left).",
                    tenant.Id,
                    daysRemaining);
            }
        }

        return new LicenseReminderRunResult(sent, skipped, failed);
    }

    public async Task<int> SendDueBillingSaleRemindersAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var pending = await _db.LicenseReminders
            .IgnoreQueryFilters()
            .Include(r => r.Tenant)
            .Include(r => r.LicenseSale)
            .Where(r =>
                r.Status == LicenseReminderStatuses.Pending
                && r.ReminderSentAtUtc == null
                && r.ReminderDateUtc <= today)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
            return 0;

        var sent = 0;

        foreach (var reminder in pending)
        {
            var tenant = reminder.Tenant;
            if (tenant == null || TenantStatuses.IsRemoved(tenant.Status))
            {
                reminder.Status = LicenseReminderStatuses.Cancelled;
                continue;
            }

            var sale = reminder.LicenseSale;
            if (sale == null || sale.Status != LicenseSaleStatuses.Active)
            {
                reminder.Status = LicenseReminderStatuses.Cancelled;
                continue;
            }

            var now = DateTime.UtcNow;
            var daysRemaining = Math.Max(0, (sale.ValidUntilUtc - now).Days);
            var (mappedDays, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
                tenant.LicenseValidUntilUtc ?? sale.ValidUntilUtc,
                tenant.LicenseKey,
                now);

            var recipients = await ResolveReminderRecipientsAsync(
                    tenant.Id,
                    tenant.Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (recipients.Count > 0)
            {
                var renewUrl = _licenseOptions.Value.AdminLicenseUrl;
                var supportEmail = ResolveSupportEmail();
                var anyDelivered = false;

                foreach (var recipient in recipients)
                {
                    var content = LicenseReminderEmailComposer.Build(
                        LicenseReminderEmailComposer.FromTenant(
                            tenant,
                            mappedDays ?? daysRemaining,
                            recipient.DisplayName,
                            renewUrl,
                            supportEmail,
                            sale.LicenseType ?? Models.Enums.LicenseType.Starter));
                    var delivered = await _emailSender
                        .TrySendTenantLicenseReminderAsync(
                            recipient.Email,
                            content.Subject,
                            content.HtmlBody,
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (delivered)
                    {
                        anyDelivered = true;
                        sent++;
                        _logger.LogInformation(
                            "License reminder sent to {Email} for tenant {TenantSlug}, days remaining: {Days}",
                            recipient.Email,
                            tenant.Slug,
                            mappedDays ?? daysRemaining);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Billing license reminder email was not delivered for tenant {TenantSlug} sale {SaleId} to {RecipientEmail}",
                            tenant.Slug,
                            sale.Id,
                            recipient.Email);
                    }
                }

                if (!anyDelivered)
                    continue;
            }
            else
            {
                _logger.LogDebug(
                    "Billing license reminder marked sent without email (no recipient) for tenant {TenantId}",
                    tenant.Id);
            }

            reminder.ReminderSentAtUtc = DateTime.UtcNow;
            reminder.Status = LicenseReminderStatuses.Sent;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sent;
    }

    private int[] ResolveMandantAnchors()
    {
        var configured = _licenseOptions.Value.ReminderDays;
        if (configured is not { Length: > 0 })
            return DefaultReminderAnchors;

        return configured
            .Where(d => d > 0)
            .Distinct()
            .OrderByDescending(d => d)
            .ToArray();
    }

    private int[] ResolveGraceAnchors()
    {
        var configured = _licenseOptions.Value.GraceReminderDays;
        if (configured is not { Length: > 0 })
            return GracePeriodReminderMilestones.DefaultReminderDays;

        return configured
            .Where(d => d >= 0)
            .Distinct()
            .OrderByDescending(d => d)
            .ToArray();
    }

    private static string BuildDedupKey(Guid tenantId, DateTime validUntilUtc, int daysBeforeExpiry) =>
        $"{tenantId:N}_{validUntilUtc:yyyyMMdd}_{daysBeforeExpiry}";

    private async Task<bool> WasReminderAlreadySentAsync(
        Guid tenantId,
        string dedupKey,
        CancellationToken cancellationToken)
    {
        return await _db.BillingAuditLogs
            .AsNoTracking()
            .AnyAsync(
                l => l.TenantId == tenantId
                     && l.Action == BillingAuditEventTypes.LicenseReminderSent
                     && l.Details != null
                     && l.Details.Contains(dedupKey, StringComparison.Ordinal),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task LogReminderSentAsync(
        Guid tenantId,
        string dedupKey,
        int daysBeforeExpiry,
        DateTime validUntilUtc,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new MandantLicenseReminderAuditDetails(
                dedupKey,
                daysBeforeExpiry,
                validUntilUtc,
                recipientEmail),
            JsonOptions);

        return _billingAudit.LogAsync(
            BillingAuditEventTypes.LicenseReminderSent,
            Guid.Empty,
            tenantId,
            saleId: null,
            details,
            ipAddress: null,
            cancellationToken);
    }

    private Task LogGraceReminderSentAsync(
        Guid tenantId,
        string dedupKey,
        int graceDaysRemaining,
        DateTime validUntilUtc,
        DateTime lockdownDateUtc,
        string recipientEmail,
        CancellationToken cancellationToken)
    {
        var details = JsonSerializer.Serialize(
            new MandantGraceReminderAuditDetails(
                dedupKey,
                graceDaysRemaining,
                validUntilUtc,
                lockdownDateUtc,
                recipientEmail),
            JsonOptions);

        return _billingAudit.LogAsync(
            BillingAuditEventTypes.LicenseReminderSent,
            Guid.Empty,
            tenantId,
            saleId: null,
            details,
            ipAddress: null,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ReminderRecipient>> ResolveReminderRecipientsAsync(
        Guid tenantId,
        string? fallbackTenantEmail,
        CancellationToken cancellationToken)
    {
        var managerRows = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { Membership = m, User = u })
            .Where(x => x.User.IsActive && x.User.Role == Roles.Manager)
            .Select(x => new
            {
                x.User.Email,
                x.User.UserName,
                x.User.FirstName,
                x.User.LastName,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var recipients = managerRows
            .Select(r => TryMapRecipient(r.Email, r.UserName, r.FirstName, r.LastName))
            .Where(r => r is not null)
            .Cast<ReminderRecipient>()
            .GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (recipients.Count > 0)
            return recipients;

        var ownerRow = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive && m.IsOwner)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (_, u) => new
                {
                    u.Email,
                    u.UserName,
                    u.FirstName,
                    u.LastName,
                })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ownerRow is not null)
        {
            var owner = TryMapRecipient(
                ownerRow.Email,
                ownerRow.UserName,
                ownerRow.FirstName,
                ownerRow.LastName);
            if (owner is not null)
                return [owner];
        }

        if (!string.IsNullOrWhiteSpace(fallbackTenantEmail)
            && fallbackTenantEmail.Contains('@'))
        {
            return [new ReminderRecipient(fallbackTenantEmail.Trim(), null)];
        }

        return [];
    }

    private static ReminderRecipient? TryMapRecipient(
        string? email,
        string? userName,
        string? firstName,
        string? lastName)
    {
        var resolved = email ?? userName;
        if (string.IsNullOrWhiteSpace(resolved) || !resolved.Contains('@'))
            return null;

        var display = $"{firstName} {lastName}".Trim();
        return new ReminderRecipient(
            resolved.Trim(),
            string.IsNullOrWhiteSpace(display) ? null : display);
    }

    private string ResolveSupportEmail()
    {
        var support = _smtpOptions.Value.SupportContact?.Trim();
        return string.IsNullOrEmpty(support)
            ? LicenseReminderEmailComposer.DefaultSupportEmail
            : support;
    }

    private sealed record ReminderRecipient(string Email, string? DisplayName);

    private sealed record MandantLicenseReminderAuditDetails(
        string DedupKey,
        int DaysBeforeExpiry,
        DateTime ValidUntilUtc,
        string RecipientEmail);

    private sealed record MandantGraceReminderAuditDetails(
        string DedupKey,
        int GraceDaysRemaining,
        DateTime ValidUntilUtc,
        DateTime LockdownDateUtc,
        string RecipientEmail);
}
