using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>Super-admin tenant creation: transactional provisioning, audit trail, optional welcome email.</summary>
public sealed class TenantOnboardingService : ITenantOnboardingService
{
    private const string AuditEntityType = "Tenant";
    private const string ActionStarted = "TENANT_ONBOARDING_STARTED";
    private const string ActionTenantCreated = "TENANT_ONBOARDING_TENANT_CREATED";
    private const string ActionProvisioned = "TENANT_ONBOARDING_PROVISIONED";
    private const string ActionWelcomeEmail = "TENANT_ONBOARDING_WELCOME_EMAIL";
    private const string ActionCompleted = "TENANT_ONBOARDING_COMPLETED";
    private const string ActionFailed = "TENANT_ONBOARDING_FAILED";

    private readonly AppDbContext _db;
    private readonly ITenantProvisioningService _provisioningService;
    private readonly IWelcomeEmailService _welcomeEmail;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<TenantOnboardingService> _logger;

    public TenantOnboardingService(
        AppDbContext db,
        ITenantProvisioningService provisioningService,
        IWelcomeEmailService welcomeEmail,
        IAuditLogService auditLog,
        ILogger<TenantOnboardingService> logger)
    {
        _db = db;
        _provisioningService = provisioningService;
        _welcomeEmail = welcomeEmail;
        _auditLog = auditLog;
        _logger = logger;
    }

    public Task<IReadOnlyList<string>> GetSlugSuggestionsAsync(
        string? companyName,
        string? preferredSlug,
        int maxCount = 5,
        CancellationToken cancellationToken = default) =>
        TenantSlugSuggestions.PickAvailableAsync(_db, companyName, preferredSlug, maxCount, cancellationToken);

    public async Task<(AdminTenantDetailDto? Result, TenantOnboardingFailureDto? Failure)> CreateAsync(
        CreateAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var actorId = actorUserId ?? "system";
        var slug = TenantSlugSuggestions.NormalizeSlug(request.Slug);

        await LogStepAsync(
            ActionStarted,
            actorId,
            correlationId,
            $"Onboarding started for slug '{slug}'",
            AuditLogStatus.Success,
            cancellationToken).ConfigureAwait(false);

        if (!TenantSlugSuggestions.IsValidSlug(slug))
        {
            var invalidFailure = new TenantOnboardingFailureDto(
                "Subdomain is invalid. Use lowercase letters, digits, and hyphens only.",
                TenantOnboardingErrorCodes.SlugInvalid,
                await GetSlugSuggestionsAsync(request.Name, request.Slug, cancellationToken: cancellationToken)
                    .ConfigureAwait(false));

            await LogStepAsync(
                ActionFailed,
                actorId,
                correlationId,
                invalidFailure.Message,
                AuditLogStatus.Failed,
                cancellationToken).ConfigureAwait(false);

            return (null, invalidFailure);
        }

        if (await _db.Tenants.AnyAsync(t => t.Slug == slug, cancellationToken).ConfigureAwait(false))
        {
            var suggestions = await GetSlugSuggestionsAsync(request.Name, slug, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var takenFailure = new TenantOnboardingFailureDto(
                $"Subdomain \"{slug}\" is already in use.",
                TenantOnboardingErrorCodes.SlugTaken,
                suggestions);

            await LogStepAsync(
                ActionFailed,
                actorId,
                correlationId,
                takenFailure.Message,
                AuditLogStatus.Failed,
                cancellationToken).ConfigureAwait(false);

            return (null, takenFailure);
        }

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Email = TrimOrNull(request.Email),
            Phone = TrimOrNull(request.Phone),
            Address = TrimOrNull(request.Address),
            Status = TenantStatuses.Active,
            IsActive = true,
            LicenseKey = TrimOrNull(request.LicenseKey),
            LicenseValidUntilUtc = request.LicenseValidUntilUtc.HasValue
                ? DateTime.SpecifyKind(request.LicenseValidUntilUtc.Value, DateTimeKind.Utc)
                : null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorUserId,
            UpdatedBy = actorUserId,
        };

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await LogStepAsync(
                ActionTenantCreated,
                actorId,
                correlationId,
                $"Tenant row created {tenant.Id}",
                AuditLogStatus.Success,
                cancellationToken,
                tenant.Id).ConfigureAwait(false);

            var (provisioning, provisionError) = await _provisioningService
                .ProvisionAsync(
                    tenant,
                    request.AdminEmail,
                    request.AdminPassword,
                    request.GrantTrialLicense,
                    request.ImportDemoMenu,
                    cancellationToken)
                .ConfigureAwait(false);

            if (provisionError != null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);

                var code = provisionError.Contains("email", StringComparison.OrdinalIgnoreCase)
                    ? TenantOnboardingErrorCodes.AdminEmailTaken
                    : TenantOnboardingErrorCodes.ProvisioningFailed;

                var failure = new TenantOnboardingFailureDto(provisionError, code);

                await LogStepAsync(
                    ActionFailed,
                    actorId,
                    correlationId,
                    provisionError,
                    AuditLogStatus.Failed,
                    cancellationToken).ConfigureAwait(false);

                return (null, failure);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            await LogStepAsync(
                ActionProvisioned,
                actorId,
                correlationId,
                $"Provisioned register {provisioning!.CashRegisterNumber}, admin {provisioning.AdminUserId}",
                AuditLogStatus.Success,
                cancellationToken,
                tenant.Id).ConfigureAwait(false);

            var portalUrl = BuildTenantPortalUrl(tenant.Slug);
            var notifyEmail = TrimOrNull(request.Email) ?? provisioning.AdminEmail;
            var welcomeSent = await _welcomeEmail.TrySendWelcomeAsync(
                new WelcomeEmailRequest(
                    notifyEmail,
                    tenant.Name,
                    tenant.Slug,
                    portalUrl,
                    provisioning.AdminEmail,
                    provisioning.GeneratedPassword,
                    ForcePasswordChangeOnNextLogin: true),
                cancellationToken).ConfigureAwait(false);

            await LogStepAsync(
                ActionWelcomeEmail,
                actorId,
                correlationId,
                welcomeSent
                    ? $"Welcome email sent to {notifyEmail}"
                    : $"Welcome email skipped (SMTP not configured or delivery failed) for {notifyEmail}",
                welcomeSent ? AuditLogStatus.Success : AuditLogStatus.Warning,
                cancellationToken,
                tenant.Id).ConfigureAwait(false);

            await LogStepAsync(
                ActionCompleted,
                actorId,
                correlationId,
                $"Onboarding completed for tenant {tenant.Id}",
                AuditLogStatus.Success,
                cancellationToken,
                tenant.Id).ConfigureAwait(false);

            _logger.LogInformation(
                "Super-admin onboarded tenant {TenantId} slug {Slug} (welcomeEmailSent={WelcomeSent})",
                tenant.Id,
                tenant.Slug,
                welcomeSent);

            var dto = provisioning.ToDto(welcomeSent, forcePasswordChangeOnNextLogin: true);
            return (ToDetail(tenant, dto), null);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(ex, "Tenant onboarding failed for slug {Slug}", slug);

            await LogStepAsync(
                ActionFailed,
                actorId,
                correlationId,
                ex.Message,
                AuditLogStatus.Failed,
                cancellationToken).ConfigureAwait(false);

            return (null, new TenantOnboardingFailureDto(
                "Tenant creation failed.",
                TenantOnboardingErrorCodes.Unknown));
        }
    }

    private async Task LogStepAsync(
        string action,
        string actorUserId,
        string correlationId,
        string description,
        AuditLogStatus status,
        CancellationToken cancellationToken,
        Guid? tenantId = null)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                action,
                AuditEntityType,
                actorUserId,
                Roles.SuperAdmin,
                description: description,
                status: status,
                correlationIdOverride: correlationId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Onboarding audit log failed for action {Action} tenant {TenantId}", action, tenantId);
        }
    }

    private static string BuildTenantPortalUrl(string slug) => $"https://{slug.Trim().ToLowerInvariant()}.regkasse.at";

    private static string? TrimOrNull(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static AdminTenantDetailDto ToDetail(Tenant t, TenantProvisioningDto provisioning) =>
        new(
            t.Id,
            t.Name,
            t.Slug,
            t.Email,
            t.Phone,
            t.Address,
            t.Status,
            t.IsActive,
            t.LicenseKey,
            t.LicenseValidUntilUtc,
            t.CreatedAt,
            t.UpdatedAt,
            t.DeletedAtUtc,
            OwnerAdminEmail: provisioning.AdminEmail,
            Provisioning: provisioning);
}
