using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data.Seed;

public interface ITenantDemoSeeder
{
    Task<TenantDemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantDemoSeedResult(int ProductsAdded, int CustomersAdded, Guid? CashRegisterId);

/// <summary>
/// Vertical demo catalog for one tenant. Matches existing rows by category key, barcode,
/// and customer number. Does not write receipts, TSE signatures, or payments.
/// </summary>
public sealed class TenantDemoSeeder : ITenantDemoSeeder
{
    private readonly AppDbContext _db;

    public TenantDemoSeeder(AppDbContext db)
    {
        _db = db;
    }

    public Task<TenantDemoSeedResult> SeedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        SeedTenantAsync(_db, tenantId, cancellationToken);

    public static async Task<TenantDemoSeedResult> SeedTenantAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.Id == tenantId && row.DeletedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return new TenantDemoSeedResult(0, 0, null);

        var now = DateTime.UtcNow;
        await EnsureTaxGroupAsync(db, tenant.Id, cancellationToken).ConfigureAwait(false);
        var taxGroupId = await db.TaxGroups
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenant.Id && row.IsActive)
            .OrderByDescending(row => row.IsDefault)
            .Select(row => row.Id)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var categories = await EnsureCategoriesAsync(db, tenant.Id, now, cancellationToken).ConfigureAwait(false);
        var productsAdded = await EnsureProductsAsync(db, tenant.Id, taxGroupId, categories, now, cancellationToken)
            .ConfigureAwait(false);
        var customersAdded = await EnsureCustomersAsync(db, tenant.Id, now, cancellationToken).ConfigureAwait(false);
        var cashRegisterId = await EnsureOpenRegisterAsync(db, tenant.Id, now, cancellationToken).ConfigureAwait(false);
        return new TenantDemoSeedResult(productsAdded, customersAdded, cashRegisterId);
    }

    private static async Task EnsureTaxGroupAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        if (await db.TaxGroups.IgnoreQueryFilters().AnyAsync(row => row.TenantId == tenantId, cancellationToken).ConfigureAwait(false))
            return;

        db.TaxGroups.AddRange(TaxGroupSeedData.GetTaxGroups(tenantId));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, Category>> EnsureCategoriesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Categories
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byKey = existing.ToDictionary(row => row.Key, StringComparer.Ordinal);

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            if (byKey.ContainsKey(profile.ProfileId))
                continue;

            var category = new Category
            {
                TenantId = tenantId,
                Key = profile.ProfileId,
                Name = profile.CategoryName,
                Description = "Demo-Katalog",
                VatRate = 20m,
                FiscalCategory = profile.FiscalCategory,
                IsSystemCategory = true,
                IsActive = true,
                SortOrder = byKey.Count + 1,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = DevTenantSeeder.CreatedBy,
            };
            db.Categories.Add(category);
            byKey[profile.ProfileId] = category;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return byKey;
    }

    private static async Task<int> EnsureProductsAsync(
        AppDbContext db,
        Guid tenantId,
        Guid taxGroupId,
        IReadOnlyDictionary<string, Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var barcodes = await db.Products
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.Barcode != null)
            .Select(row => row.Barcode!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = barcodes.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            var category = categories[profile.ProfileId];
            for (var index = 0; index < profile.ProductNames.Count; index++)
            {
                var barcode = DevTenantSeeder.Barcode(profile.ProfileId, index + 1);
                if (!known.Add(barcode))
                    continue;

                db.Products.Add(new Product
                {
                    TenantId = tenantId,
                    Name = profile.ProductNames[index],
                    Description = profile.CategoryName,
                    Price = 9.90m + index,
                    TaxType = (int)TaxType.Standard,
                    TaxRate = 20m,
                    Category = category.Name,
                    CategoryId = category.Id,
                    TaxGroupId = taxGroupId,
                    Barcode = barcode,
                    IsActive = true,
                    StockQuantity = 50,
                    MinStockLevel = 0,
                    Unit = "Stk",
                    Cost = 0,
                    IsFiscalCompliant = true,
                    IsTaxable = true,
                    RksvProductType = RksvProductTypes.Standard,
                    IsSellableAddOn = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                    UpdatedBy = DevTenantSeeder.CreatedBy,
                });
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task<int> EnsureCustomersAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var numbers = await db.Customers
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.CustomerNumber != "")
            .Select(row => row.CustomerNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var known = numbers.ToHashSet(StringComparer.Ordinal);
        var added = 0;

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            for (var index = 0; index < profile.CustomerNames.Count; index++)
            {
                var number = DevTenantSeeder.CustomerNumber(profile.ProfileId, index + 1);
                if (!known.Add(number))
                    continue;

                db.Customers.Add(new Customer
                {
                    TenantId = tenantId,
                    Name = profile.CustomerNames[index],
                    CustomerNumber = number,
                    Email = "",
                    Phone = "",
                    Address = "",
                    TaxNumber = "",
                    Notes = "",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                });
                added++;
            }
        }

        if (added > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return added;
    }

    private static async Task<Guid> EnsureOpenRegisterAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var registers = await db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive && row.DecommissionedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var register = registers.FirstOrDefault(row => row.Status == RegisterStatus.Open);
        if (register is null)
        {
            register = registers.FirstOrDefault(row => row.IsDefaultForTenant)
                ?? registers.FirstOrDefault(row => row.Status == RegisterStatus.Closed);
            if (register is null)
            {
                register = new CashRegister
                {
                    TenantId = tenantId,
                    RegisterNumber = DevTenantSeeder.RegisterNumber,
                    Location = "Hauptkasse",
                    StartingBalance = 0,
                    CurrentBalance = 0,
                    LastBalanceUpdate = now,
                    Status = RegisterStatus.Open,
                    IsDefaultForTenant = true,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = DevTenantSeeder.CreatedBy,
                };
                db.CashRegisters.Add(register);
            }
            else if (register.Status != RegisterStatus.Decommissioned)
            {
                register.Status = RegisterStatus.Open;
                register.LastBalanceUpdate = now;
                register.UpdatedAt = now;
                register.UpdatedBy = DevTenantSeeder.CreatedBy;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var hasOpenShift = await db.CashierShifts
            .IgnoreQueryFilters()
            .AnyAsync(
                row => row.CashRegisterId == register.Id
                    && row.Status == CashierShiftStatuses.Active
                    && row.EndedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!hasOpenShift)
        {
            db.CashierShifts.Add(new CashierShift
            {
                TenantId = tenantId,
                CashRegisterId = register.Id,
                CashierId = DemoTenantAdminUserIds.Dev,
                CashierName = "Demo Kasse",
                StartBalance = register.CurrentBalance,
                StartedAt = now,
                Status = CashierShiftStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = DevTenantSeeder.CreatedBy,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return register.Id;
    }
}
