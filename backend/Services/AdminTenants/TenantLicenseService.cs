using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantLicenseService : ITenantLicenseService
{
    private readonly AppDbContext _db;
    private readonly ILicenseKeyGenerator _licenseKeyGenerator;

    public TenantLicenseService(AppDbContext db, ILicenseKeyGenerator licenseKeyGenerator)
    {
        _db = db;
        _licenseKeyGenerator = licenseKeyGenerator;
    }

    public async Task<(LicensePreviewResult? Result, string? Error)> PreviewLicenseAsync(
        Guid tenantId,
        string licenseKey,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return (TenantLicensePreviewHelper.BuildInvalidPreview(
                new IssuedLicenseResolveResult(null, "invalid_key", "licenseKey is required.")), null);
        }

        if (!isSuperAdmin)
        {
            var billingResolved = await ResolveBillingLicenseSaleForKeyAsync(tenantId, licenseKey, cancellationToken)
                .ConfigureAwait(false);
            return billingResolved.ErrorCode == null
                ? (TenantLicensePreviewHelper.BuildValidPreview(billingResolved.Sale!), null)
                : (TenantLicensePreviewHelper.BuildInvalidPreview(billingResolved), null);
        }

        var resolved = await ResolveIssuedLicenseForKeyAsync(
                tenantId,
                licenseKey,
                isSuperAdmin,
                cancellationToken)
            .ConfigureAwait(false);

        return resolved.ErrorCode == null
            ? (TenantLicensePreviewHelper.BuildValidPreview(resolved.Issued!), null)
            : (TenantLicensePreviewHelper.BuildInvalidPreview(resolved), null);
    }

    public async Task<BillingLicenseSaleResolveResult> ResolveBillingLicenseSaleForKeyAsync(
        Guid tenantId,
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var key = licenseKey.Trim();
        if (!_licenseKeyGenerator.ValidateLicenseKeyFormat(key))
        {
            return new BillingLicenseSaleResolveResult(
                null,
                "invalid_key",
                LicenseKeyGenerator.InvalidFormatMessage);
        }

        var sale = await _db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.LicenseKey == key)
            .OrderByDescending(s => s.SoldAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sale == null)
        {
            return new BillingLicenseSaleResolveResult(
                null,
                "not_found",
                "License key was not found.");
        }

        if (sale.TenantId != tenantId)
        {
            return new BillingLicenseSaleResolveResult(
                sale,
                "wrong_tenant",
                "This license key is not valid for this tenant.");
        }

        if (!string.Equals(sale.Status, LicenseSaleStatuses.Active, StringComparison.Ordinal))
        {
            return new BillingLicenseSaleResolveResult(
                sale,
                "cancelled",
                "This license sale is no longer active.");
        }

        if (sale.ValidUntilUtc <= DateTime.UtcNow)
        {
            return new BillingLicenseSaleResolveResult(
                sale,
                "expired",
                "This license key has expired.");
        }

        return new BillingLicenseSaleResolveResult(sale, null, null);
    }

    public async Task<IssuedLicenseResolveResult> ResolveIssuedLicenseForKeyAsync(
        Guid tenantId,
        string licenseKey,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        var key = licenseKey.Trim();
        if (!RegkTenantLicenseKeyFormat.IsValid(key))
        {
            return new IssuedLicenseResolveResult(
                null,
                "invalid_key",
                RegkTenantLicenseKeyFormat.InvalidFormatMessage);
        }

        var now = DateTime.UtcNow;
        var issued = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.LicenseKey == key && !il.IsDeleted && !il.IsRevoked && !il.IsCancelled && il.SupersededByLicenseId == null)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (issued == null)
        {
            return new IssuedLicenseResolveResult(
                null,
                "not_found",
                "Issued license key was not found.");
        }

        if (issued.ExpiryAtUtc <= now)
        {
            return new IssuedLicenseResolveResult(
                issued,
                "expired",
                "Issued license key has expired.");
        }

        if (!isSuperAdmin && !TenantLicenseLink.IsIssuedLicenseAssignableToTenant(issued.CustomerName, tenantId))
        {
            return new IssuedLicenseResolveResult(
                issued,
                "wrong_tenant",
                "This license key is not valid for this tenant.");
        }

        var assignedToOtherTenant = await _db.Tenants.AsNoTracking()
            .AnyAsync(
                t => t.Id != tenantId
                     && t.LicenseKey == key
                     && t.Status != TenantStatuses.Deleted
                     && (t.LicenseValidUntilUtc == null || t.LicenseValidUntilUtc > now),
                cancellationToken)
            .ConfigureAwait(false);
        if (assignedToOtherTenant)
        {
            return new IssuedLicenseResolveResult(
                issued,
                "already_used",
                "This license key is already assigned to another tenant.");
        }

        return new IssuedLicenseResolveResult(issued, null, null);
    }
}
