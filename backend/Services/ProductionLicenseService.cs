using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services;

/// <summary>
/// Production-facing <see cref="ILicenseService"/> adapter that delegates all operations to the
/// process-wide <see cref="LicenseService"/> singleton (trial, offline JWT, remote validation, revocation overlay).
/// When an HTTP request resolves a mandant via <see cref="ICurrentTenantAccessor"/>, public status uses
/// <see cref="Tenant.LicenseValidUntilUtc"/> as the authoritative expiry (SaaS mandant license).
/// </summary>
/// <remarks>
/// <para>
/// In non-development hosting this type is registered as <strong>scoped</strong> so <see cref="ILicenseService"/>
/// can be resolved per HTTP request; all authoritative deployment state remains on the inner singleton <see cref="LicenseService"/>.
/// </para>
/// <para>
/// During OpenAPI export, the same adapter is registered as a <strong>singleton</strong> so <see cref="ILicenseService"/>
/// can be resolved when no request scope exists.
/// </para>
/// </remarks>
public sealed class ProductionLicenseService : ILicenseService
{
    private static readonly TenantLicenseValidator TenantLicenseValidator = new();

    private readonly LicenseService _inner;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Creates an adapter around the shared <see cref="LicenseService"/> implementation.</summary>
    public ProductionLicenseService(
        LicenseService inner,
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory)
    {
        _inner = inner;
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public void EvaluateOnStartup() => _inner.EvaluateOnStartup();

    /// <inheritdoc />
    public LicenseStatusResponse GetStatus() =>
        ApplyTenantMandantOverlayIfPresent(_inner.GetStatus());

    /// <inheritdoc />
    public LicenseStatusResponse GetDeploymentStatus() => _inner.GetStatus();

    /// <inheritdoc />
    public async Task<LicenseStatusResponse> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        var deployment = await _inner.GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
        return ApplyTenantMandantOverlayIfPresent(deployment);
    }

    /// <inheritdoc />
    public Task<LicenseStatusResponse> GetCurrentDeploymentStatusAsync(CancellationToken cancellationToken = default) =>
        _inner.GetCurrentStatusAsync(cancellationToken);

    /// <inheritdoc />
    public bool IsLicenseSnapshotInitialized => _inner.IsLicenseSnapshotInitialized;

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        if (ResolveRequestTenantId() is Guid tenantId && tenantId != Guid.Empty
            && await TenantHasMandantLicenseExpiryAsync(tenantId, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenant = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (tenant == null)
                return await _inner.ValidateAsync(cancellationToken).ConfigureAwait(false);

            var isSuperAdmin = IsSuperAdminRequest();
            var permissions = TenantLicenseValidator.GetPermissions(tenant.LicenseValidUntilUtc, isSuperAdmin);
            var s = await GetCurrentStatusAsync(cancellationToken).ConfigureAwait(false);
            var paid = s.IsValid && !s.IsTrial;
            var trialActive = s.IsTrial && !s.IsExpired;
            return new LicenseValidationResult
            {
                IsLicenseOperational = permissions.CanAccess,
                IsTrial = s.IsTrial,
                IsExpired = s.IsExpired,
                IsPaidValid = paid,
                DaysRemaining = s.DaysRemaining,
                ExpiryUtc = s.ExpiryDate,
            };
        }

        return await _inner.ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<LicenseActivationResult> ActivateAsync(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default) =>
        ActivateAsyncInner(request, clientInfo, cancellationToken);

    /// <inheritdoc />
    public async Task<LicenseStatusInfo> GetLicenseStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var info = await _inner.GetLicenseStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!IsSuperAdminRequest())
            return info;

        info.CanAccess = true;
        info.CanTransact = true;
        info.RequiresRenewal = false;
        return info;
    }

    private async Task<LicenseActivationResult> ActivateAsyncInner(
        ActivateLicenseRequest request,
        LicenseActivationClientInfo? clientInfo = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ActivateAsync(request, clientInfo, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return result;

        using (var scope = _scopeFactory.CreateScope())
        {
            var licenseSync = scope.ServiceProvider.GetRequiredService<ILicenseSyncService>();
            if (!string.IsNullOrWhiteSpace(request.LicenseKey))
            {
                await licenseSync
                    .SyncTenantsForLicenseKeyAsync(request.LicenseKey.Trim(), cancellationToken)
                    .ConfigureAwait(false);
            }

            if (ResolveRequestTenantId() is Guid tenantId && tenantId != Guid.Empty)
            {
                await licenseSync
                    .SyncTenantLicenseExpiryAsync(tenantId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return result;
    }

    private LicenseStatusResponse ApplyTenantMandantOverlayIfPresent(LicenseStatusResponse deployment)
    {
        var tenantId = ResolveRequestTenantId();
        if (tenantId is null || tenantId == Guid.Empty)
            return deployment;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenant = db.Tenants
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefault(t => t.Id == tenantId);
            if (tenant == null)
                return deployment;

            var overlay = TenantLicenseStatusMapper.TryMapToLicenseStatus(tenant, deployment.MachineHash);
            return overlay ?? deployment;
        }
        catch
        {
            return deployment;
        }
    }

    private async Task<bool> TenantHasMandantLicenseExpiryAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return tenant?.LicenseValidUntilUtc != null;
    }

    private async Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    private Guid? ResolveRequestTenantId()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null)
            return null;

        var accessor = http.RequestServices.GetService<ICurrentTenantAccessor>();
        return accessor?.TenantId;
    }

    private bool IsSuperAdminRequest()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return false;

        return principal.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => RoleCanonicalization.GetCanonicalRole(c.Value))
            .Any(r => string.Equals(r, Roles.SuperAdmin, StringComparison.Ordinal));
    }
}
