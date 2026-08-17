using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using IBillingTenantLicenseService = KasseAPI_Final.Services.Billing.ITenantLicenseService;

namespace KasseAPI_Final.Services.License;

/// <summary>
/// Unification layer for REGK keys: combined status, slug-based activation, and
/// validation against both <c>issued_licenses</c> and <c>license_sales</c>.
/// Inner persistence stays on <see cref="ILicenseService"/> (deployment adapter)
/// and billing <c>ITenantLicenseService</c> (mandant adapter).
/// </summary>
public sealed class UnifiedLicenseService : IUnifiedLicenseService
{
    private readonly AppDbContext _db;
    private readonly ILicenseService _deployment;
    private readonly IBillingTenantLicenseService _billing;
    private readonly ILicenseStatusCache _licenseStatusCache;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<UnifiedLicenseService> _logger;

    public UnifiedLicenseService(
        AppDbContext db,
        ILicenseService deployment,
        IBillingTenantLicenseService billing,
        ILicenseStatusCache licenseStatusCache,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<UnifiedLicenseService> logger)
    {
        _db = db;
        _deployment = deployment;
        _billing = billing;
        _licenseStatusCache = licenseStatusCache;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    public async Task<UnifiedLicenseStatusDto> GetUnifiedStatusAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTenantId = tenantId is Guid requested && requested != Guid.Empty
            ? requested
            : _tenantAccessor.TenantId is Guid ambient && ambient != Guid.Empty
                ? ambient
                : (Guid?)null;

        var deployment = await _deployment
            .GetCurrentStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        var systemLayer = MapSystemLayer(deployment);

        LicenseStatusInfo? mandant = null;
        if (resolvedTenantId is Guid tid)
        {
            mandant = await _deployment
                .GetLicenseStatusAsync(tid, cancellationToken)
                .ConfigureAwait(false);
        }

        var tenantLayer = MapTenantLayer(mandant);
        var slug = systemLayer.IsActive && !tenantLayer.IsActive
            ? LicenseKeyGenerator.SystemSlug
            : await ResolveAmbientTenantSlugAsync(resolvedTenantId, cancellationToken)
                .ConfigureAwait(false)
              ?? (systemLayer.IsActive ? LicenseKeyGenerator.SystemSlug : string.Empty);

        var licenseType = tenantLayer.IsActive
            ? LicenseKeyKinds.Tenant
            : systemLayer.IsActive
                ? LicenseKeyKinds.System
                : resolvedTenantId is not null
                    ? LicenseKeyKinds.Tenant
                    : LicenseKeyKinds.System;

        var validUntil = tenantLayer.IsActive
            ? tenantLayer.ValidUntil ?? systemLayer.ValidUntil
            : systemLayer.ValidUntil ?? tenantLayer.ValidUntil;

        var combinedStatus = tenantLayer.IsActive && string.Equals(tenantLayer.Status, "grace", StringComparison.Ordinal)
            ? "grace"
            : systemLayer.IsActive || tenantLayer.IsActive
                ? "active"
                : "expired";

        return new UnifiedLicenseStatusDto
        {
            IsActive = systemLayer.IsActive || tenantLayer.IsActive,
            LicenseType = licenseType,
            Slug = slug,
            ValidUntil = validUntil,
            IsSystemLicense = systemLayer.IsActive,
            IsTenantLicense = tenantLayer.IsActive,
            SystemLicense = systemLayer,
            TenantLicense = tenantLayer,
            Status = combinedStatus,
            DeploymentSnapshot = deployment,
            MandantSnapshot = mandant,
        };
    }

    public async Task<LicenseKeyValidationResult> ValidateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        var tenantId = _tenantAccessor.TenantId;
        var expectedTenantId = tenantId is Guid id && id != Guid.Empty ? id : (Guid?)null;
        if (expectedTenantId is null
            && LicenseKeyGenerator.IsMandantBillingKey(resolved.CanonicalKey)
            && !string.IsNullOrEmpty(resolved.Slug))
        {
            expectedTenantId = await FindTenantIdBySlugAsync(resolved.Slug, cancellationToken)
                .ConfigureAwait(false);
        }

        var ambientSlug = await ResolveAmbientTenantSlugAsync(expectedTenantId, cancellationToken)
            .ConfigureAwait(false);
        return ToValidation(resolved, expectedTenantId, ambientSlug);
    }

    public Task<LicenseActivationResult> ActivateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default) =>
        ActivateLicenseAsync(licenseKey, BuildDefaultActivationContext(), cancellationToken);

    public async Task<LicenseActivationResult> ActivateLicenseAsync(
        string licenseKey,
        UnifiedLicenseActivationContext context,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        var requestedTenantId = context.TenantId is Guid ctxTenant && ctxTenant != Guid.Empty
            ? ctxTenant
            : context.DeploymentRequest?.TenantId is Guid bodyTenant && bodyTenant != Guid.Empty
                ? bodyTenant
                : (Guid?)null;

        var isMandantKey = LicenseKeyGenerator.IsMandantBillingKey(resolved.CanonicalKey)
            || LicenseKeyGenerator.IsMandantBillingKey(resolved.InputKey);

        Guid? activationTenantId = requestedTenantId;
        if (isMandantKey && activationTenantId is null)
        {
            activationTenantId = await FindTenantIdBySlugAsync(resolved.Slug, cancellationToken)
                .ConfigureAwait(false);
        }

        var ambientSlug = await ResolveAmbientTenantSlugAsync(activationTenantId, cancellationToken)
            .ConfigureAwait(false);
        var validation = ToValidation(resolved, isMandantKey ? activationTenantId : null, ambientSlug);

        if (!validation.IsFormatValid)
        {
            return new LicenseActivationResult(false, validation.Message ?? LicenseKeyGenerator.InvalidFormatMessage);
        }

        if (validation.IsExpired)
        {
            return new LicenseActivationResult(
                false,
                validation.Message ?? "This license has expired.");
        }

        if (isMandantKey)
        {
            if (activationTenantId is not Guid tenantId || tenantId == Guid.Empty)
                return new LicenseActivationResult(false, "Tenant context required.");

            if (context.ActorUserId is not Guid userId || userId == Guid.Empty)
            {
                return new LicenseActivationResult(
                    false,
                    "Authentication required for mandant license activation.");
            }

            if (!validation.SlugMatches)
            {
                return new LicenseActivationResult(
                    false,
                    "Dieser Lizenzschlüssel ist für einen anderen Mandanten ausgestellt.");
            }

            var billing = await _billing
                .ActivateLicenseAsync(tenantId, resolved.CanonicalKey, userId, cancellationToken)
                .ConfigureAwait(false);

            if (!billing.Success)
                return new LicenseActivationResult(false, billing.Message);

            await _licenseStatusCache
                .InvalidateLicenseCacheAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);

            return new LicenseActivationResult(
                true,
                billing.Message,
                billing.ValidUntilUtc,
                billing.LicensePlan,
                TenantId: tenantId,
                TenantSlug: ambientSlug ?? resolved.Slug,
                DaysRemaining: LicenseService.ComputeActivationDaysRemaining(billing.ValidUntilUtc),
                Status: "active");
        }

        var request = context.DeploymentRequest ?? new ActivateLicenseRequest { LicenseKey = resolved.InputKey };
        if (string.IsNullOrWhiteSpace(request.LicenseKey))
            request.LicenseKey = resolved.InputKey;

        var deploymentResult = await _deployment
            .ActivateAsync(request, context.ClientInfo, cancellationToken)
            .ConfigureAwait(false);

        if (deploymentResult is null)
            return new LicenseActivationResult(false, "Activation failed due to an internal error.");

        if (deploymentResult.Success && activationTenantId is Guid cacheTenantId && cacheTenantId != Guid.Empty)
        {
            await _licenseStatusCache
                .InvalidateLicenseCacheAsync(cacheTenantId, cancellationToken)
                .ConfigureAwait(false);
        }

        return deploymentResult;
    }

    public Task<LicenseDeactivationResult> DeactivateLicenseAsync(
        string licenseKey,
        CancellationToken cancellationToken = default) =>
        DeactivateLicenseAsync(licenseKey, context: null, cancellationToken);

    public async Task<LicenseDeactivationResult> DeactivateLicenseAsync(
        string licenseKey,
        UnifiedLicenseDeactivationContext? context,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        if (!resolved.FormatOk)
        {
            return new LicenseDeactivationResult
            {
                Success = false,
                Message = resolved.Message ?? LicenseKeyGenerator.InvalidFormatMessage,
                CanonicalLicenseKey = resolved.CanonicalKey,
            };
        }

        var deactivatedAny = false;
        string? kind = resolved.Kind;

        if (resolved.Issued is not null)
        {
            if (resolved.Issued.IsRevoked)
            {
                return new LicenseDeactivationResult
                {
                    Success = false,
                    Message = "This license is already revoked.",
                    LicenseKind = LicenseKeyKinds.System,
                    CanonicalLicenseKey = resolved.CanonicalKey,
                };
            }

            var now = DateTime.UtcNow;
            resolved.Issued.IsRevoked = true;
            resolved.Issued.RevokedAtUtc = now;
            resolved.Issued.RevokedByUserId = context?.ActorUserId?.ToString("D");
            if (!string.IsNullOrWhiteSpace(context?.Reason))
            {
                var trimmed = context.Reason.Trim();
                resolved.Issued.RevocationReason = trimmed.Length > 512 ? trimmed[..512] : trimmed;
            }

            var keys = new[] { resolved.InputKey, resolved.CanonicalKey }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var activations = await _db.ActivatedLicenses
                .Where(a => keys.Contains(a.LicenseKey) && a.IsActive)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in activations)
                row.IsActive = false;

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            deactivatedAny = true;
            kind = resolved.Sale is not null ? LicenseKeyKinds.Both : LicenseKeyKinds.System;
        }

        if (resolved.Sale is not null)
        {
            if (string.Equals(resolved.Sale.Status, LicenseSaleStatuses.Cancelled, StringComparison.Ordinal))
            {
                if (deactivatedAny)
                {
                    return new LicenseDeactivationResult
                    {
                        Success = true,
                        Message = "Issued license revoked.",
                        LicenseKind = kind,
                        CanonicalLicenseKey = resolved.CanonicalKey,
                    };
                }

                return new LicenseDeactivationResult
                {
                    Success = false,
                    Message = "This license is already cancelled.",
                    LicenseKind = LicenseKeyKinds.Tenant,
                    CanonicalLicenseKey = resolved.CanonicalKey,
                };
            }

            var saleNow = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(context?.Reason)
                ? "Deactivated via unified license service."
                : context.Reason.Trim();
            resolved.Sale.Status = LicenseSaleStatuses.Cancelled;
            resolved.Sale.CancelledAtUtc = saleNow;
            resolved.Sale.CancelledByUserId = context?.ActorUserId;
            resolved.Sale.CancellationReason = reason.Length > 500 ? reason[..500] : reason;
            resolved.Sale.UpdatedAt = saleNow;

            var tenant = resolved.Sale.Tenant
                ?? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == resolved.Sale.TenantId, cancellationToken)
                    .ConfigureAwait(false);
            if (tenant is not null && tenant.CurrentLicenseSaleId == resolved.Sale.Id)
            {
                tenant.CurrentLicenseSaleId = null;
                tenant.LicenseKey = null;
                tenant.LicenseValidUntilUtc = null;
                tenant.UpdatedAt = saleNow;
            }

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _licenseStatusCache
                .InvalidateLicenseCacheAsync(resolved.Sale.TenantId, cancellationToken)
                .ConfigureAwait(false);

            deactivatedAny = true;
            kind = resolved.Issued is not null ? LicenseKeyKinds.Both : LicenseKeyKinds.Tenant;
        }

        if (!deactivatedAny)
        {
            return new LicenseDeactivationResult
            {
                Success = false,
                Message = "No issued license matches this license key.",
                CanonicalLicenseKey = resolved.CanonicalKey,
            };
        }

        return new LicenseDeactivationResult
        {
            Success = true,
            Message = "License deactivated.",
            LicenseKind = kind,
            CanonicalLicenseKey = resolved.CanonicalKey,
        };
    }

    public async Task<bool> IsLicenseValidAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateLicenseAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        return result.IsValid;
    }

    public async Task<LicenseInfo> GetLicenseInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(licenseKey, cancellationToken).ConfigureAwait(false);
        var validation = ToValidation(resolved, expectedTenantId: null, ambientSlug: null);
        var dbUntil = resolved.Issued?.ExpiryAtUtc ?? resolved.Sale?.ValidUntilUtc;

        return new LicenseInfo
        {
            LicenseKey = resolved.InputKey,
            CanonicalLicenseKey = resolved.CanonicalKey,
            LicenseKind = resolved.Kind ?? LicenseKeyKinds.Tenant,
            Slug = resolved.Slug,
            Exists = resolved.Issued is not null || resolved.Sale is not null,
            IsValid = validation.IsValid,
            IsExpired = validation.IsExpired,
            IsRevoked = resolved.Issued?.IsRevoked == true
                || string.Equals(resolved.Sale?.Status, LicenseSaleStatuses.Cancelled, StringComparison.Ordinal),
            ValidUntilUtc = dbUntil ?? resolved.EncodedValidUntilUtc,
            TenantId = resolved.Sale?.TenantId,
            TenantSlug = resolved.Sale?.Tenant?.Slug ?? resolved.Slug,
            CustomerName = resolved.Issued?.CustomerName,
            SourceTable = resolved.Issued is not null
                ? "issued_licenses"
                : resolved.Sale is not null
                    ? "license_sales"
                    : null,
            SourceId = resolved.Issued?.Id ?? resolved.Sale?.Id,
            Status = resolved.Issued is not null
                ? (resolved.Issued.IsRevoked ? "revoked" : "issued")
                : resolved.Sale?.Status,
        };
    }

    private UnifiedLicenseActivationContext BuildDefaultActivationContext()
    {
        var tenantId = _tenantAccessor.TenantId;
        return new UnifiedLicenseActivationContext(
            TenantId: tenantId is Guid id && id != Guid.Empty ? id : null,
            ActorUserId: null);
    }

    private async Task<ResolvedLicense> ResolveAsync(string licenseKey, CancellationToken cancellationToken)
    {
        var input = (licenseKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(input))
        {
            return new ResolvedLicense
            {
                InputKey = input,
                CanonicalKey = input,
                FormatOk = false,
                Message = "licenseKey is required.",
            };
        }

        var canonical = await ResolveCanonicalKeyAsync(input, cancellationToken).ConfigureAwait(false);
        var unified = LicenseKeyGenerator.TryParseLicenseKey(canonical, out var slug, out var encodedUntil, out _)
            || LicenseKeyGenerator.TryParseLicenseKey(input, out slug, out encodedUntil, out _);
        var legacyDisplay = RegkTenantLicenseKeyFormat.IsValid(input) || RegkTenantLicenseKeyFormat.IsValid(canonical);

        if (!unified && !legacyDisplay)
        {
            return new ResolvedLicense
            {
                InputKey = input,
                CanonicalKey = canonical,
                FormatOk = false,
                Message = LicenseKeyGenerator.InvalidFormatMessage,
            };
        }

        var issued = await FindIssuedAsync(input, canonical, cancellationToken).ConfigureAwait(false);
        var sale = await FindSaleAsync(input, canonical, cancellationToken).ConfigureAwait(false);

        string kind;
        if (issued is not null && sale is not null)
            kind = LicenseKeyKinds.Both;
        else if (issued is not null || LicenseKeyGenerator.IsSystemLicenseKey(canonical) || LicenseKeyGenerator.IsDeploymentLicenseKey(input))
            kind = LicenseKeyKinds.System;
        else
            kind = LicenseKeyKinds.Tenant;

        if (LicenseKeyGenerator.IsMandantBillingKey(canonical) || LicenseKeyGenerator.IsMandantBillingKey(input))
            kind = sale is not null && issued is not null ? LicenseKeyKinds.Both : LicenseKeyKinds.Tenant;

        DateTime? encodedUtc = unified
            ? DateTime.SpecifyKind(encodedUntil.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc)
            : null;

        return new ResolvedLicense
        {
            InputKey = input,
            CanonicalKey = canonical,
            FormatOk = true,
            Kind = kind,
            Slug = unified ? slug : null,
            EncodedValidUntilUtc = encodedUtc,
            Issued = issued,
            Sale = sale,
        };
    }

    private async Task<string?> ResolveAmbientTenantSlugAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId is not Guid id || id == Guid.Empty)
            return null;

        return await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == id)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Guid?> FindTenantIdBySlugAsync(string? slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug)
            || string.Equals(slug, LicenseKeyGenerator.SystemSlug, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var id = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Slug == normalized)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return id == Guid.Empty ? null : id;
    }

    private static UnifiedLicenseLayerStatusDto MapSystemLayer(LicenseStatusResponse deployment)
    {
        var paid = deployment.IsValid && !deployment.IsTrial;
        var trialActive = deployment.IsTrial && !deployment.IsExpired;
        var active = paid || trialActive;
        return new UnifiedLicenseLayerStatusDto
        {
            ValidUntil = deployment.ExpiryDate,
            Status = active ? "active" : "expired",
            IsActive = active,
        };
    }

    private static UnifiedLicenseLayerStatusDto MapTenantLayer(LicenseStatusInfo? mandant)
    {
        if (mandant is null)
        {
            return new UnifiedLicenseLayerStatusDto
            {
                Status = "expired",
                IsActive = false,
            };
        }

        var active = mandant.CanAccess && !mandant.IsLocked;
        var status = !active
            ? "expired"
            : mandant.IsInGracePeriod
                ? "grace"
                : "active";
        return new UnifiedLicenseLayerStatusDto
        {
            ValidUntil = mandant.ValidUntil,
            Status = status,
            IsActive = active,
        };
    }

    private static LicenseKeyValidationResult ToValidation(
        ResolvedLicense resolved,
        Guid? expectedTenantId = null,
        string? ambientSlug = null)
    {
        if (!resolved.FormatOk)
        {
            return new LicenseKeyValidationResult
            {
                IsValid = false,
                IsFormatValid = false,
                CanonicalLicenseKey = resolved.CanonicalKey,
                ErrorCode = "invalid_format",
                Message = resolved.Message,
            };
        }

        var exists = resolved.Issued is not null || resolved.Sale is not null;
        var now = DateTime.UtcNow;
        var dbUntil = resolved.Issued?.ExpiryAtUtc ?? resolved.Sale?.ValidUntilUtc;
        var encodedExpired = resolved.EncodedValidUntilUtc is DateTime encoded && encoded < now;
        var dbExpired = dbUntil is DateTime until
            && (until.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(until, DateTimeKind.Utc)
                : until.ToUniversalTime()) <= now;
        var isExpired = encodedExpired || dbExpired;

        var slugMatches = true;
        if (IsTenantKind(resolved.Kind) && !string.IsNullOrEmpty(resolved.Slug))
        {
            if (!string.IsNullOrEmpty(ambientSlug)
                && !string.Equals(ambientSlug, resolved.Slug, StringComparison.OrdinalIgnoreCase))
            {
                slugMatches = false;
            }

            if (expectedTenantId is Guid tenantId && tenantId != Guid.Empty && resolved.Sale is not null)
                slugMatches &= resolved.Sale.TenantId == tenantId;

            if (resolved.Sale?.Tenant?.Slug is string tenantSlug
                && !string.Equals(tenantSlug, resolved.Slug, StringComparison.OrdinalIgnoreCase))
            {
                slugMatches = false;
            }
        }

        var revoked = resolved.Issued?.IsRevoked == true
            || resolved.Issued?.IsCancelled == true
            || resolved.Issued?.IsDeleted == true
            || string.Equals(resolved.Sale?.Status, LicenseSaleStatuses.Cancelled, StringComparison.Ordinal);

        string? errorCode = null;
        string? message = null;
        if (isExpired)
        {
            errorCode = "expired";
            message = "This license has expired.";
        }
        else if (!exists)
        {
            errorCode = "not_found";
            message = "License key not found.";
        }
        else if (revoked)
        {
            errorCode = "revoked";
            message = "This license is no longer valid.";
        }
        else if (!slugMatches)
        {
            errorCode = "slug_mismatch";
            message = "License slug does not match the tenant.";
        }

        var isValid = exists && !isExpired && !revoked && slugMatches;

        return new LicenseKeyValidationResult
        {
            IsValid = isValid,
            IsFormatValid = true,
            ExistsInDatabase = exists,
            IsExpired = isExpired,
            SlugMatches = slugMatches,
            LicenseKind = resolved.Kind,
            CanonicalLicenseKey = resolved.CanonicalKey,
            Slug = resolved.Slug,
            EncodedValidUntilUtc = resolved.EncodedValidUntilUtc,
            DatabaseValidUntilUtc = dbUntil,
            ErrorCode = errorCode,
            Message = message,
        };
    }

    private static bool IsTenantKind(string? kind) =>
        string.Equals(kind, LicenseKeyKinds.Tenant, StringComparison.Ordinal)
        || string.Equals(kind, LicenseKeyKinds.Both, StringComparison.Ordinal);

    private async Task<string> ResolveCanonicalKeyAsync(string licenseKey, CancellationToken cancellationToken)
    {
        try
        {
            var mapped = await _db.LicenseKeyMappings.AsNoTracking()
                .Where(m => m.OldLicenseKey == licenseKey || m.NewLicenseKey == licenseKey)
                .Select(m => m.NewLicenseKey)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            return string.IsNullOrEmpty(mapped) ? licenseKey : mapped;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unified license: mapping lookup skipped.");
            return licenseKey;
        }
    }

    private Task<IssuedLicense?> FindIssuedAsync(string input, string canonical, CancellationToken cancellationToken) =>
        _db.IssuedLicenses
            .FirstOrDefaultAsync(
                il => il.LicenseKey == canonical || il.LicenseKey == input,
                cancellationToken);

    private Task<LicenseSale?> FindSaleAsync(string input, string canonical, CancellationToken cancellationToken) =>
        _db.LicenseSales
            .IgnoreQueryFilters()
            .Include(s => s.Tenant)
            .FirstOrDefaultAsync(
                s => s.LicenseKey == canonical || s.LicenseKey == input,
                cancellationToken);

    private sealed class ResolvedLicense
    {
        public required string InputKey { get; init; }
        public required string CanonicalKey { get; init; }
        public bool FormatOk { get; init; }
        public string? Kind { get; init; }
        public string? Slug { get; init; }
        public DateTime? EncodedValidUntilUtc { get; init; }
        public IssuedLicense? Issued { get; init; }
        public LicenseSale? Sale { get; init; }
        public string? Message { get; init; }
    }
}
