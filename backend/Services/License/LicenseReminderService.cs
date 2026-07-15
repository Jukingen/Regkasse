using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Sends scheduled mandant license expiry emails at configured calendar-day anchors before expiry.
/// </summary>
public sealed class LicenseReminderService : ILicenseReminderService
{
    private static readonly int[] DefaultReminderAnchors = [30, 15, 7, 3, 1];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ILicenseReminderEmailSender _emailSender;
    private readonly IBillingAuditService _billingAudit;
    private readonly IOptions<LicenseOptions> _licenseOptions;
    private readonly ILogger<LicenseReminderService> _logger;

    public LicenseReminderService(
        AppDbContext db,
        ILicenseReminderEmailSender emailSender,
        IBillingAuditService billingAudit,
        IOptions<LicenseOptions> licenseOptions,
        ILogger<LicenseReminderService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _billingAudit = billingAudit;
        _licenseOptions = licenseOptions;
        _logger = logger;
    }

    public async Task<LicenseReminderRunResult> SendDueMandantExpiryRemindersAsync(
        CancellationToken cancellationToken = default)
    {
        var anchors = ResolveMandantAnchors();
        if (anchors.Length == 0)
            return new LicenseReminderRunResult(0, 0, 0);

        var maxAnchor = anchors.Max();
        var now = DateTime.UtcNow;

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.Status == TenantStatuses.Active
                && t.LicenseValidUntilUtc != null
                && t.LicenseValidUntilUtc > now
                && t.LicenseValidUntilUtc <= now.AddDays(maxAnchor + 1))
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

            if (kind != "active" || daysRemaining is not > 0 || !anchors.Contains(daysRemaining.Value))
            {
                skipped++;
                continue;
            }

            var validUntil = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc!.Value, DateTimeKind.Utc);
            var dedupKey = BuildDedupKey(tenant.Id, validUntil, daysRemaining.Value);

            if (await WasReminderAlreadySentAsync(tenant.Id, dedupKey, cancellationToken).ConfigureAwait(false))
            {
                skipped++;
                continue;
            }

            var recipient = await ResolveReminderRecipientEmailAsync(
                    tenant.Id,
                    tenant.Email,
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(recipient))
            {
                _logger.LogDebug(
                    "Skipping mandant license reminder for tenant {TenantId}: no recipient email.",
                    tenant.Id);
                skipped++;
                continue;
            }

            var subject = LicenseReminderEmailComposer.BuildMandantExpirySubject(tenant.Name, daysRemaining.Value);
            var body = LicenseReminderEmailComposer.BuildMandantExpiryBody(tenant, daysRemaining, kind);
            var delivered = await _emailSender
                .TrySendTenantLicenseReminderAsync(recipient, subject, body, cancellationToken)
                .ConfigureAwait(false);

            if (!delivered)
            {
                failed++;
                continue;
            }

            await LogReminderSentAsync(
                    tenant.Id,
                    dedupKey,
                    daysRemaining.Value,
                    validUntil,
                    recipient,
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Mandant license expiry reminder sent for tenant {TenantId} ({DaysRemaining}d) to {RecipientEmail}",
                tenant.Id,
                daysRemaining.Value,
                recipient);

            sent++;
        }

        return new LicenseReminderRunResult(sent, skipped, failed);
    }

    public async Task<int> SendDueBillingSaleRemindersAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var pending = await _db.LicenseReminders
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
            if (tenant == null || tenant.Status == TenantStatuses.Deleted)
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

            var recipient = await ResolveReminderRecipientEmailAsync(
                    tenant.Id,
                    tenant.Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(recipient))
            {
                var subject = LicenseReminderEmailComposer.BuildMandantExpirySubject(
                    tenant.Name,
                    mappedDays ?? daysRemaining);
                var body = LicenseReminderEmailComposer.BuildMandantExpiryBody(
                    tenant,
                    mappedDays ?? daysRemaining,
                    kind);
                var delivered = await _emailSender
                    .TrySendTenantLicenseReminderAsync(recipient, subject, body, cancellationToken)
                    .ConfigureAwait(false);

                if (delivered)
                {
                    sent++;
                    _logger.LogInformation(
                        "Billing license reminder email sent for tenant {TenantSlug} sale {SaleId}",
                        tenant.Slug,
                        sale.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Billing license reminder email was not delivered for tenant {TenantSlug} sale {SaleId}",
                        tenant.Slug,
                        sale.Id);
                    continue;
                }
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

    private async Task<string?> ResolveReminderRecipientEmailAsync(
        Guid tenantId,
        string? fallbackTenantEmail,
        CancellationToken cancellationToken)
    {
        var ownerEmail = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsActive && m.IsOwner)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (_, u) => u.Email ?? u.UserName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(ownerEmail))
            return ownerEmail.Trim();

        return string.IsNullOrWhiteSpace(fallbackTenantEmail) ? null : fallbackTenantEmail.Trim();
    }

    private sealed record MandantLicenseReminderAuditDetails(
        string DedupKey,
        int DaysBeforeExpiry,
        DateTime ValidUntilUtc,
        string RecipientEmail);
}
