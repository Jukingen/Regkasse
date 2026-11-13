using System.Net;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Trial;

/// <summary>
/// Converts an open SaaS trial tenant to a paid <see cref="LicenseSale"/> without deleting business data.
/// </summary>
public sealed class TrialConversionService : ITrialConversionService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly IActivityEventService _activity;
    private readonly IBillingAuditService _billingAudit;
    private readonly IAuditLogService _auditLog;
    private readonly IOptionsMonitor<LicenseOptions> _licenseOptions;
    private readonly ILogger<TrialConversionService> _logger;

    public TrialConversionService(
        AppDbContext db,
        IEmailService email,
        IActivityEventService activity,
        IBillingAuditService billingAudit,
        IAuditLogService auditLog,
        IOptionsMonitor<LicenseOptions> licenseOptions,
        ILogger<TrialConversionService> logger)
    {
        _db = db;
        _email = email;
        _activity = activity;
        _billingAudit = billingAudit;
        _auditLog = auditLog;
        _licenseOptions = licenseOptions;
        _logger = logger;
    }

    public async Task<(TrialConversionResult? Result, string? Error)> ConvertToPaidAsync(
        Guid tenantId,
        Guid licenseSaleId,
        bool addRemainingTrialDays = true,
        string? notes = null,
        string? actorUserId = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default)
    {
        if (SystemTenantIds.IsPlatformTenantId(tenantId))
            return (null, "Tenant not found.");

        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null || TenantStatuses.IsRemoved(tenant.Status))
            return (null, "Tenant not found.");

        if (!TrialStatuses.IsOpenTrial(tenant.TrialStatus))
            return (null, "Tenant is not in an open trial (active or expired grace).");

        var sale = await _db.LicenseSales
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == licenseSaleId, cancellationToken)
            .ConfigureAwait(false);
        if (sale == null || sale.TenantId != tenantId)
            return (null, "License sale not found for this tenant.");
        if (!string.Equals(sale.Status, LicenseSaleStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return (null, "License sale is not active.");

        var now = DateTime.UtcNow;
        var trialEnds = tenant.TrialEndsAtUtc.HasValue
            ? DateTime.SpecifyKind(tenant.TrialEndsAtUtc.Value, DateTimeKind.Utc)
            : (DateTime?)null;
        var remainingDays = 0;
        if (addRemainingTrialDays && trialEnds.HasValue && trialEnds.Value > now)
        {
            remainingDays = Math.Max(0, (int)Math.Ceiling((trialEnds.Value - now).TotalDays));
        }

        var baseUntil = DateTime.SpecifyKind(sale.ValidUntilUtc, DateTimeKind.Utc);
        var finalUntil = remainingDays > 0 ? baseUntil.AddDays(remainingDays) : baseUntil;

        sale.ValidUntilUtc = finalUntil;
        if (sale.CustomValidUntilUtc.HasValue)
            sale.CustomValidUntilUtc = finalUntil;
        sale.ConvertedFromTrial = true;
        sale.RemainingTrialDaysAdded = remainingDays > 0 ? remainingDays : null;
        sale.TrialConvertedAtUtc = now;
        sale.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            var noteLine = $"[trial-conversion] {notes.Trim()}";
            sale.Notes = string.IsNullOrWhiteSpace(sale.Notes)
                ? noteLine
                : $"{sale.Notes}\n{noteLine}";
        }

        var trialDurationDays = tenant.TrialStartedAtUtc.HasValue && tenant.TrialEndsAtUtc.HasValue
            ? Math.Max(0, (int)Math.Ceiling(
                (DateTime.SpecifyKind(tenant.TrialEndsAtUtc.Value, DateTimeKind.Utc)
                 - DateTime.SpecifyKind(tenant.TrialStartedAtUtc.Value, DateTimeKind.Utc)).TotalDays))
            : (int?)null;
        var daysUsed = tenant.TrialStartedAtUtc.HasValue
            ? Math.Max(0, (int)Math.Ceiling(
                (now - DateTime.SpecifyKind(tenant.TrialStartedAtUtc.Value, DateTimeKind.Utc)).TotalDays))
            : (int?)null;

        tenant.TrialStatus = TrialStatuses.Converted;
        tenant.TrialConvertedAtUtc = now;
        tenant.TrialGracePeriodEndsAtUtc = null;
        tenant.CurrentLicenseSaleId = sale.Id;
        tenant.LicenseKey = sale.LicenseKey;
        tenant.LicenseValidUntilUtc = finalUntil;
        tenant.LastLicenseActivationUtc = now;
        tenant.LicenseActivationCount += 1;
        if (!string.Equals(tenant.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase))
            tenant.Status = TenantStatuses.Active;
        tenant.IsActive = true;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var metadataJson = JsonSerializer.Serialize(new
        {
            licenseSaleId = sale.Id,
            licensePlan = sale.LicensePlan,
            licenseType = sale.LicenseType?.ToString(),
            trialDurationDays,
            daysUsed,
            remainingDaysAdded = remainingDays,
            licenseValidUntilUtc = finalUntil,
        });

        try
        {
            await _billingAudit.LogAsync(
                BillingAuditEventTypes.TrialConverted,
                ParseActorGuid(actorUserId),
                tenantId,
                sale.Id,
                details: metadataJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Billing audit failed for trial conversion {TenantId}", tenantId);
        }

        try
        {
            await _auditLog.LogSystemOperationAsync(
                "TRIAL_CONVERTED",
                "Tenant",
                actorUserId ?? "system",
                actorRole ?? Roles.SuperAdmin,
                description: $"Trial converted to paid sale {sale.Id}; remainingDaysAdded={remainingDays}",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.TrialConvertedToPaid,
                entityId: tenantId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Security audit failed for trial conversion {TenantId}", tenantId);
        }

        try
        {
            await _activity.PublishAsync(
                new ActivityEventPublishRequest(
                    tenantId,
                    ActivityEventType.TrialConverted,
                    "Trial converted to paid",
                    $"Plan {sale.LicensePlan}; valid until {finalUntil:yyyy-MM-dd} UTC"
                    + (remainingDays > 0 ? $"; +{remainingDays} remaining trial day(s)" : string.Empty),
                    ActorUserId: actorUserId,
                    EntityType: "LicenseSale",
                    EntityId: sale.Id.ToString("D"),
                    DedupKey: $"trial-converted:{tenantId:N}:{sale.Id:N}",
                    Metadata: new Dictionary<string, object>
                    {
                        ["licenseSaleId"] = sale.Id.ToString("D"),
                        ["licensePlan"] = sale.LicensePlan,
                        ["remainingDaysAdded"] = remainingDays,
                        ["licenseValidUntilUtc"] = finalUntil.ToString("o"),
                    }),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity publish failed for trial conversion {TenantId}", tenantId);
        }

        await SendCustomerWelcomeAsync(tenant, sale, finalUntil, remainingDays, cancellationToken)
            .ConfigureAwait(false);
        await SendSuperAdminNotifyAsync(tenant, sale, finalUntil, remainingDays, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant {TenantId} converted from trial to paid. License valid until {ValidUntil}",
            tenantId,
            finalUntil);

        var result = new TrialConversionResult(
            Success: true,
            TenantId: tenantId,
            LicenseSaleId: sale.Id,
            LicenseValidUntilUtc: finalUntil,
            ConversionDateUtc: now,
            RemainingTrialDaysAdded: remainingDays,
            LicensePlan: sale.LicensePlan,
            LicenseKey: sale.LicenseKey,
            Message: "Trial successfully converted to paid license");

        return (result, null);
    }

    private async Task SendCustomerWelcomeAsync(
        Tenant tenant,
        LicenseSale sale,
        DateTime validUntilUtc,
        int remainingDays,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant.Email))
            return;

        var name = WebUtility.HtmlEncode(tenant.Name);
        var remainingNote = remainingDays > 0
            ? $"<p>{remainingDays} remaining trial day(s) were added to your license.</p>"
            : string.Empty;
        var body =
            $"<p>Welcome to Regkasse!</p>"
            + $"<p>Your trial for <strong>{name}</strong> has been upgraded to a paid license ({WebUtility.HtmlEncode(sale.LicensePlan)}).</p>"
            + $"<p>License valid until <strong>{validUntilUtc:yyyy-MM-dd} UTC</strong>.</p>"
            + remainingNote
            + "<p>Getting started: open your admin portal, change the provisioned password if prompted, and complete the onboarding checklist.</p>";

        await _email.TrySendHtmlAsync(
            tenant.Email,
            "Welcome to Regkasse! Your trial has been upgraded",
            body,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SendSuperAdminNotifyAsync(
        Tenant tenant,
        LicenseSale sale,
        DateTime validUntilUtc,
        int remainingDays,
        CancellationToken cancellationToken)
    {
        var dashboardUrl = _licenseOptions.CurrentValue.AdminDashboardUrl?.Trim();
        // Best-effort: reuse From address as ops inbox when no dedicated report recipient exists.
        // Super Admin inbox is not a first-class config; skip if SMTP From is missing.
        if (!_email.IsConfigured)
            return;

        // Prefer notifying via activity feed; optional HTML to SMTP From as ops mirror is noisy — skip.
        _logger.LogInformation(
            "Trial converted: tenant={TenantId} slug={Slug} plan={Plan} until={Until} remainingDays={Days} dashboard={Url}",
            tenant.Id,
            tenant.Slug,
            sale.LicensePlan,
            validUntilUtc,
            remainingDays,
            dashboardUrl);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static Guid ParseActorGuid(string? actorUserId)
    {
        if (Guid.TryParse(actorUserId, out var id))
            return id;
        return Guid.Empty;
    }
}
