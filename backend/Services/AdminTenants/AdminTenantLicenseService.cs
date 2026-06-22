using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class AdminTenantLicenseService : IAdminTenantLicenseService
{
    public static class Tiers
    {
        public const string Basic = "basic";
        public const string Standard = "standard";
        public const string Premium = "premium";

        public static bool IsKnown(string tier) =>
            string.Equals(tier, Basic, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, Standard, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, Premium, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> FeaturesFor(string tier)
        {
            if (string.Equals(tier, Basic, StringComparison.OrdinalIgnoreCase))
                return [LicenseFeatureIds.PosFiscal, LicenseFeatureIds.AdminBasic];
            if (string.Equals(tier, Standard, StringComparison.OrdinalIgnoreCase))
                return
                [
                    LicenseFeatureIds.PosFiscal,
                    LicenseFeatureIds.PosOffline,
                    LicenseFeatureIds.AdminBasic,
                    LicenseFeatureIds.AdminRksv,
                ];
            if (string.Equals(tier, Premium, StringComparison.OrdinalIgnoreCase))
                return LicenseFeatureIds.All;
            throw new ArgumentException($"Unknown tier: {tier}", nameof(tier));
        }
    }

    private readonly AppDbContext _db;
    private readonly ILicenseSyncService _licenseSync;
    private readonly ILicenseIssuanceService _licenseIssuance;
    private readonly ILicenseReminderEmailSender _licenseReminderEmailSender;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminTenantLicenseService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly LicenseOptions _licenseOptions;
    private readonly IDevelopmentModeService _developmentModeService;
    private readonly ITenantLicenseService _tenantLicenseService;

    public AdminTenantLicenseService(
        AppDbContext db,
        ILicenseSyncService licenseSync,
        ILicenseIssuanceService licenseIssuance,
        ILicenseReminderEmailSender licenseReminderEmailSender,
        IAuditLogService auditLogService,
        ILogger<AdminTenantLicenseService> logger,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IOptions<LicenseOptions> licenseOptions,
        IDevelopmentModeService developmentModeService,
        ITenantLicenseService tenantLicenseService)
    {
        _db = db;
        _licenseSync = licenseSync;
        _licenseIssuance = licenseIssuance;
        _licenseReminderEmailSender = licenseReminderEmailSender;
        _auditLogService = auditLogService;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _licenseOptions = licenseOptions.Value;
        _developmentModeService = developmentModeService;
        _tenantLicenseService = tenantLicenseService;
    }

    public async Task<TenantLicenseOverviewDto?> GetOverviewAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return null;

        var history = await BuildHistoryAsync(tenant, cancellationToken).ConfigureAwait(false);
        var features = await ResolveFeaturesAsync(tenant, cancellationToken).ConfigureAwait(false);
        return new TenantLicenseOverviewDto(BuildStatus(tenant, history, features), history);
    }

    public async Task<IReadOnlyList<TenantLicenseOverviewListItemDto>> ListOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var tenants = await _db.Tenants.AsNoTracking()
            .Where(t => t.Status != TenantStatuses.Deleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
            return Array.Empty<TenantLicenseOverviewListItemDto>();

        var tenantIds = tenants.Select(t => t.Id).ToList();
        var tenantsWithOwner = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => tenantIds.Contains(m.TenantId) && m.IsActive && m.IsOwner)
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var ownerTenantIds = tenantsWithOwner.ToHashSet();
        var now = DateTime.UtcNow;

        return tenants
            .Select(t => new TenantLicenseOverviewListItemDto(
                t.Id,
                t.Name,
                t.Slug,
                t.LicenseKey,
                t.LicenseValidUntilUtc,
                TenantLicenseOverviewStatusMapper.ResolveStatus(t.LicenseValidUntilUtc, t.LicenseKey, now),
                ownerTenantIds.Contains(t.Id),
                t.CreatedAt))
            .ToList();
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> ActivateTrialAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot receive a trial license.");

        var now = DateTime.UtcNow;
        tenant.LicenseValidUntilUtc = now.AddDays(30);
        tenant.LicenseKey = null;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Super-admin activated 30-day trial for tenant {TenantId}", tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> ExtendAsync(
        Guid tenantId,
        ExtendTenantLicenseRequest request,
        string? actorUserId,
        string? actorRole = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        var isSuperAdmin = string.Equals(actorRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
        if (!isSuperAdmin)
        {
            if (request.ValidUntilUtc.HasValue)
                return (null, "validUntilUtc is determined by the license key and cannot be set manually.");
            if (string.IsNullOrWhiteSpace(request.LicenseKey))
                return (null, "licenseKey is required.");
        }

        var now = DateTime.UtcNow;
        var previousValidUntil = tenant.LicenseValidUntilUtc;
        var previousKey = tenant.LicenseKey;

        if (!string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            var key = request.LicenseKey.Trim();

            if (!isSuperAdmin)
            {
                var billing = await _tenantLicenseService.ResolveBillingLicenseSaleForKeyAsync(
                        tenantId,
                        key,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (billing.ErrorCode != null)
                    return (null, billing.ErrorMessage);

                tenant.LicenseKey = key;
                tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(billing.Sale!.ValidUntilUtc, DateTimeKind.Utc);
            }
            else
            {
                if (!RegkTenantLicenseKeyFormat.IsValid(key))
                    return (null, RegkTenantLicenseKeyFormat.InvalidFormatMessage);

                tenant.LicenseKey = key;

                if (request.ValidUntilUtc.HasValue)
                {
                    tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc);
                }
                else
                {
                    var resolved = await _tenantLicenseService.ResolveIssuedLicenseForKeyAsync(
                            tenantId,
                            key,
                            isSuperAdmin,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (resolved.ErrorCode != null)
                        return (null, resolved.ErrorMessage);

                    tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(resolved.Issued!.ExpiryAtUtc, DateTimeKind.Utc);
                }
            }
        }
        else if (request.ValidUntilUtc.HasValue)
        {
            tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc);
        }
        else
        {
            return (null, "Provide licenseKey or validUntilUtc.");
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(tenant.LicenseKey))
            await _licenseSync.SyncTenantLicenseExpiryAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await LogLicenseExtendedAuditAsync(
            tenantId,
            actorUserId,
            actorRole,
            previousKey,
            previousValidUntil,
            tenant.LicenseKey,
            tenant.LicenseValidUntilUtc,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Tenant license extended for tenant {TenantId}", tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    private async Task LogLicenseExtendedAuditAsync(
        Guid tenantId,
        string? actorUserId,
        string? actorRole,
        string? previousKey,
        DateTime? previousValidUntilUtc,
        string? newKey,
        DateTime? newValidUntilUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return;

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                AuditLogActions.LICENSE_EXTENDED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                actorUserId,
                actorRole ?? "Manager",
                description: "Tenant license extended.",
                requestData: new
                {
                    tenantId,
                    old_key = MaskLicenseKeyForAudit(previousKey),
                    new_key = MaskLicenseKeyForAudit(newKey),
                    old_valid_until_utc = previousValidUntilUtc,
                    new_valid_until_utc = newValidUntilUtc,
                },
                actionType: AuditEventType.LicenseExtended,
                entityId: tenantId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for tenant license extend TenantId={TenantId}", tenantId);
        }

        _ = cancellationToken;
    }

    private async Task LogLicenseUpdatedAuditAsync(
        Guid tenantId,
        string? actorUserId,
        string? actorRole,
        string? previousKey,
        DateTime? previousValidUntilUtc,
        string? newKey,
        DateTime? newValidUntilUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            return;

        try
        {
            await _auditLogService.LogSystemOperationAsync(
                AuditLogActions.LICENSE_UPDATED,
                AuditLogEntityTypes.SYSTEM_CONFIG,
                actorUserId,
                actorRole ?? "Manager",
                description: "Tenant license updated.",
                requestData: new
                {
                    tenantId,
                    old_key = MaskLicenseKeyForAudit(previousKey),
                    new_key = MaskLicenseKeyForAudit(newKey),
                    old_valid_until_utc = previousValidUntilUtc,
                    new_valid_until_utc = newValidUntilUtc,
                },
                actionType: AuditEventType.LicenseUpdated,
                entityId: tenantId,
                tenantId: tenantId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit log failed for tenant license update TenantId={TenantId}", tenantId);
        }

        _ = cancellationToken;
    }

    private static string? MaskLicenseKeyForAudit(string? licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return null;
        var trimmed = licenseKey.Trim();
        if (trimmed.Length <= 8)
            return trimmed;
        return $"{trimmed[..8]}…";
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> SetTierAsync(
        Guid tenantId,
        SetTenantLicenseTierRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Tiers.IsKnown(request.Tier))
            return (null, "Invalid tier. Use basic, standard, or premium.");

        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        var now = DateTime.UtcNow;
        var currentEnd = tenant.LicenseValidUntilUtc;
        var baseInstant = currentEnd.HasValue && currentEnd.Value > now ? currentEnd.Value : now;
        tenant.LicenseValidUntilUtc = request.ValidUntilUtc.HasValue
            ? DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc)
            : baseInstant.AddDays(365);

        if (string.IsNullOrWhiteSpace(tenant.LicenseKey))
            tenant.LicenseKey = $"TIER:{request.Tier.Trim().ToLowerInvariant()}";

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Super-admin set license tier {Tier} for tenant {TenantId}",
            request.Tier,
            tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<(TenantLicenseConsistencyDto? Result, string? Error)> CheckDeploymentConsistencyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be checked.");

        var warnings = new List<string>();
        var linked = await FindLinkedIssuedLicensesAsync(tenant, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var active = linked
            .Where(il => LicenseSyncService.IsActiveIssuedLicense(il) && il.ExpiryAtUtc > now)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefault();

        if (!tenant.LicenseValidUntilUtc.HasValue && string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            warnings.Add("Mandant has no license end date or key; nothing to align with deployment licenses.");
            return (new TenantLicenseConsistencyDto(
                IsConsistent: warnings.Count == 0,
                warnings,
                tenant.LicenseValidUntilUtc,
                null,
                null,
                null,
                CanIssueDeploymentLicense: false), null);
        }

        if (active == null)
        {
            warnings.Add(
                "No active issued_licenses row is linked to this tenant (by license key, customer name, or [tenant:guid] marker).");
        }
        else
        {
            var tenantKey = tenant.LicenseKey?.Trim();
            if (!string.IsNullOrEmpty(tenantKey)
                && !IsSyntheticTierKey(tenantKey)
                && !string.Equals(tenantKey, active.LicenseKey, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"Mandant license key differs from linked issued license ({MaskKey(active.LicenseKey)}).");
            }

            if (tenant.LicenseValidUntilUtc.HasValue)
            {
                var tenantExpiry = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
                var issuedExpiry = DateTime.SpecifyKind(active.ExpiryAtUtc, DateTimeKind.Utc);
                if (Math.Abs((tenantExpiry - issuedExpiry).TotalHours) > 1)
                {
                    warnings.Add(
                        $"Mandant valid_until ({tenantExpiry:yyyy-MM-dd HH:mm} UTC) differs from issued expiry ({issuedExpiry:yyyy-MM-dd HH:mm} UTC).");
                }
            }
        }

        var canIssue = active == null
            && tenant.LicenseValidUntilUtc.HasValue
            && tenant.LicenseValidUntilUtc.Value > now;

        return (new TenantLicenseConsistencyDto(
            IsConsistent: warnings.Count == 0,
            warnings,
            tenant.LicenseValidUntilUtc,
            active?.Id,
            active?.LicenseKey,
            active?.ExpiryAtUtc,
            canIssue), null);
    }

    public async Task<(TenantLicenseIssueDeploymentResultDto? Result, string? Error)> IssueDeploymentLicenseAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var (check, checkError) = await CheckDeploymentConsistencyAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (checkError != null)
            return (null, checkError);
        if (check == null)
            return (null, "Consistency check failed.");
        if (!check.CanIssueDeploymentLicense)
            return (null, "Deployment license cannot be issued: mandant has no future end date or an active issued row already exists.");

        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var expiry = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc!.Value, DateTimeKind.Utc);
        GenerateLicenseResult issued;
        try
        {
            issued = await _licenseIssuance.IssueAsync(
                    new GenerateLicenseRequest(
                        TenantLicenseLink.BuildCustomerName(tenant),
                        expiry,
                        RequireFingerprint: false,
                        MachineHashHex: null,
                        FeatureIds: Tiers.FeaturesFor(Tiers.Basic)),
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Deployment license issuance unavailable for tenant {TenantId}", tenantId);
            return (null, ex.Message);
        }

        if (!issued.Success || string.IsNullOrWhiteSpace(issued.LicenseKey))
            return (null, issued.Message ?? "Failed to issue deployment license.");

        tenant.LicenseKey = issued.LicenseKey.Trim();
        tenant.LicenseValidUntilUtc = issued.ExpiryAtUtc.HasValue
            ? DateTime.SpecifyKind(issued.ExpiryAtUtc.Value, DateTimeKind.Utc)
            : expiry;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var issuedRow = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.LicenseKey == tenant.LicenseKey)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Select(il => il.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Issued deployment license for tenant {TenantId} slug {Slug} keyPrefix {Prefix}",
            tenantId,
            tenant.Slug,
            MaskKey(tenant.LicenseKey));

        var overview = await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return (new TenantLicenseIssueDeploymentResultDto(
            true,
            "Deployment license (JWT) created and linked to mandant.",
            tenant.LicenseKey,
            issuedRow == default ? null : issuedRow,
            overview), null);
    }

    public async Task<(TenantLicenseReminderResultDto? Result, string? Error)> SendReminderEmailAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot receive license reminders.");

        var recipientEmail = await ResolveReminderRecipientEmailAsync(tenantId, tenant.Email, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(recipientEmail))
            return (null, "Tenant has no owner or contact email for reminders.");

        var (daysRemaining, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
            tenant.LicenseValidUntilUtc,
            tenant.LicenseKey);

        var subject = $"Regkasse Lizenz-Erinnerung - {tenant.Name}";
        var body = BuildReminderEmailBody(tenant, daysRemaining, kind);
        var sent = await _licenseReminderEmailSender
            .TrySendTenantLicenseReminderAsync(recipientEmail, subject, body, cancellationToken)
            .ConfigureAwait(false);
        if (!sent)
            return (null, "SMTP is not configured or the reminder email could not be delivered.");

        _logger.LogInformation(
            "Tenant license reminder email sent for tenant {TenantId} to {RecipientEmail} by {ActorUserId}",
            tenantId,
            recipientEmail,
            actorUserId ?? "(unknown)");

        return (new TenantLicenseReminderResultDto(
            true,
            recipientEmail,
            "Reminder email sent successfully."), null);
    }

    private async Task<List<IssuedLicense>> FindLinkedIssuedLicensesAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var marker = TenantLicenseLink.Marker(tenant.Id);
        var key = tenant.LicenseKey?.Trim();

        var query = _db.IssuedLicenses.AsNoTracking().Where(il => !il.IsDeleted);

        if (!string.IsNullOrEmpty(key) && !IsSyntheticTierKey(key))
        {
            query = query.Where(il =>
                il.LicenseKey == key
                || il.CustomerName == tenant.Name
                || EF.Functions.ILike(il.CustomerName, $"%{marker}%"));
        }
        else
        {
            query = query.Where(il =>
                il.CustomerName == tenant.Name
                || EF.Functions.ILike(il.CustomerName, $"%{marker}%"));
        }

        return await query
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsSyntheticTierKey(string key) =>
        key.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase);

    private static string MaskKey(string key) =>
        key.Length <= 16 ? key : key[..16] + "…";

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
            return ownerEmail;

        return string.IsNullOrWhiteSpace(fallbackTenantEmail) ? null : fallbackTenantEmail.Trim();
    }

    private static string BuildReminderEmailBody(Tenant tenant, int? daysRemaining, string kind)
    {
        var validUntilLabel = tenant.LicenseValidUntilUtc?.ToString("dd.MM.yyyy") ?? "—";
        var statusLabel = kind switch
        {
            "active" => "Aktiv",
            "grace_write" => "Grace Write",
            "grace_read_only" => "Grace ReadOnly",
            "lockdown" => "Lockdown",
            "no_license" => "Keine Lizenz",
            _ => kind,
        };
        var remainingLabel = daysRemaining.HasValue ? daysRemaining.Value.ToString() : "—";

        return string.Join(Environment.NewLine,
        [
            "Guten Tag,",
            string.Empty,
            $"für den Mandanten \"{tenant.Name}\" wurde eine Lizenz-Erinnerung ausgelöst.",
            string.Empty,
            $"Mandant: {tenant.Name}",
            $"Subdomain: {tenant.Slug}",
            $"Lizenzstatus: {statusLabel}",
            $"Gültig bis: {validUntilLabel}",
            $"Verbleibende Tage: {remainingLabel}",
            string.Empty,
            "Bitte prüfen Sie die Lizenzverlängerung im Regkasse Adminbereich.",
        ]);
    }

    private async Task<Tenant?> LoadMutableTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<TenantLicenseHistoryItemDto>> BuildHistoryAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var items = new List<TenantLicenseHistoryItemDto>();

        if (tenant.LicenseValidUntilUtc.HasValue && string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            items.Add(new TenantLicenseHistoryItemDto(
                null,
                "trial",
                tenant.UpdatedAt ?? tenant.CreatedAt,
                $"Demo license until {tenant.LicenseValidUntilUtc:yyyy-MM-dd} UTC",
                null));
        }

        if (!string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            var issuedRows = await _db.IssuedLicenses.AsNoTracking()
                .Where(il => il.LicenseKey == tenant.LicenseKey && !il.IsDeleted)
                .OrderByDescending(il => il.IssuedAtUtc)
                .Take(20)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in issuedRows)
            {
                var features = LicenseFeatureIds.TryParseStoredFeatures(row.FeaturesJson);
                var tier = InferTier(features);
                items.Add(new TenantLicenseHistoryItemDto(
                    row.Id,
                    row.IsRevoked ? "revoked" : "issued",
                    row.IssuedAtUtc,
                    $"Issued to {row.CustomerName} until {row.ExpiryAtUtc:yyyy-MM-dd} UTC"
                        + (tier != null ? $" ({tier})" : string.Empty),
                    row.LicenseKey));
            }
        }

        var byName = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted && il.CustomerName == tenant.Name)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var marker = TenantLicenseLink.Marker(tenant.Id);
        var byMarker = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted && il.CustomerName != null && il.CustomerName.Contains(marker))
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in byName.Concat(byMarker).Where(r => items.All(i => i.IssuedLicenseId != r.Id)))
        {
            items.Add(new TenantLicenseHistoryItemDto(
                row.Id,
                "issued",
                row.IssuedAtUtc,
                $"Issued (name match) until {row.ExpiryAtUtc:yyyy-MM-dd} UTC",
                row.LicenseKey));
        }

        return items
            .OrderByDescending(i => i.AtUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> ResolveFeaturesAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var key = tenant.LicenseKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return Tiers.FeaturesFor(Tiers.Basic);

        if (key.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase))
        {
            var tier = key["TIER:".Length..];
            if (Tiers.IsKnown(tier))
                return Tiers.FeaturesFor(tier);
        }

        var issued = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.LicenseKey == key && !il.IsDeleted)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return issued == null
            ? Tiers.FeaturesFor(Tiers.Premium)
            : LicenseFeatureIds.ParseJsonArrayOrDefault(issued.FeaturesJson);
    }

    private TenantLicenseStatusDto BuildStatus(
        Tenant tenant,
        IReadOnlyList<TenantLicenseHistoryItemDto> history,
        IReadOnlyList<string> features)
    {
        _ = history;

        if (LicenseEnforcementPolicy.ShouldDisableEnforcement(
                _hostEnvironment,
                _tseOptions,
                _developmentModeService,
                _licenseOptions))
        {
            return new TenantLicenseStatusDto(
                "active",
                tenant.LicenseKey,
                tenant.LicenseValidUntilUtc,
                999,
                InferTier(features),
                features);
        }

        var (days, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
            tenant.LicenseValidUntilUtc,
            tenant.LicenseKey);

        return new TenantLicenseStatusDto(
            kind,
            tenant.LicenseKey,
            tenant.LicenseValidUntilUtc,
            days,
            InferTier(features),
            features);
    }

    private static string? InferTier(IReadOnlyList<string>? features)
    {
        if (features == null || features.Count == 0)
            return null;
        if (features.Count >= LicenseFeatureIds.All.Count)
            return Tiers.Premium;
        if (features.Contains(LicenseFeatureIds.AdminRksv, StringComparer.OrdinalIgnoreCase))
            return Tiers.Standard;
        return Tiers.Basic;
    }
}
