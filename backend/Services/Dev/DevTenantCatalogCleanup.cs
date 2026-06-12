using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Dev;

public static class DevTenantCatalogCleanup
{
    public const string ConfirmPhrase = "DEV-PURGE-CATALOG";
    public const string FiscalOverridePhrase = "DEV-PURGE-CATALOG-WITH-FISCAL";

    public static async Task<DevTenantCatalogCleanupResult> ExecuteAsync(
        AppDbContext db,
        Guid tenantId,
        bool includeCategories,
        bool allowFiscalOverride,
        CancellationToken cancellationToken = default)
    {
        var tenantExists = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (!tenantExists)
            throw new InvalidOperationException("Tenant not found.");

        // RKSV: only signed receipts (TSE) block hard catalog purge; unsigned dev/test payments are ignored.
        var hasFiscalPayments = await db.PaymentDetails.IgnoreQueryFilters().AsNoTracking()
            .Join(
                db.CashRegisters.IgnoreQueryFilters().AsNoTracking(),
                payment => payment.CashRegisterId,
                register => register.Id,
                (payment, register) => new { payment, register })
            .AnyAsync(
                row => row.register.TenantId == tenantId
                    && row.payment.TseSignature != null
                    && row.payment.TseSignature != "",
                cancellationToken)
            .ConfigureAwait(false);

        if (hasFiscalPayments && !allowFiscalOverride)
        {
            throw new InvalidOperationException(
                "Tenant has signed fiscal payment records (RKSV/TSE). Use confirm phrase DEV-PURGE-CATALOG-WITH-FISCAL only when you accept audit risk in development.");
        }

        var products = await db.Products
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var productIds = products.Select(p => p.Id).ToList();

        if (productIds.Count > 0)
        {
            var favorites = await db.CashierFavorites
                .IgnoreQueryFilters()
                .Where(f => f.TenantId == tenantId && productIds.Contains(f.ProductId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (favorites.Count > 0)
                db.CashierFavorites.RemoveRange(favorites);

            db.Products.RemoveRange(products);
        }

        var categoriesDeleted = 0;
        if (includeCategories)
        {
            var categories = await db.Categories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            categoriesDeleted = categories.Count;
            if (categories.Count > 0)
                db.Categories.RemoveRange(categories);
        }

        if (products.Count > 0 || categoriesDeleted > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DevTenantCatalogCleanupResult(
            tenantId,
            products.Count,
            categoriesDeleted,
            HasFiscalPayments: hasFiscalPayments);
    }
}

public sealed record DevTenantCatalogCleanupResult(
    Guid TenantId,
    int ProductsDeleted,
    int CategoriesDeleted,
    bool HasFiscalPayments);
