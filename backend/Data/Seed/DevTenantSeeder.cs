using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data.Seed;

/// <summary>
/// Additive Development catalog for the <c>dev</c> tenant so each vertical profile
/// has a few sellable products, named customers, and an open register shift.
/// Does not create fiscal receipts or change an existing profile assignment.
/// </summary>
public static class DevTenantSeeder
{
    public const string RegisterNumber = "KASSE-001";
    public const string CreatedBy = "dev-tenant-seed";

    public static IReadOnlyList<DevTenantProfileCatalog> Catalog { get; } =
    [
        Profile(VerticalProfileIds.Gastronomy, "Gastronomie", RksvProductCategory.Food,
            ["Wiener Schnitzel", "Gulasch", "Cola", "Apfelstrudel"],
            ["Anna Berger", "Lukas Hofer"]),
        Profile(VerticalProfileIds.GastronomyTables, "Gastronomie Tische", RksvProductCategory.Food,
            ["Tagesmenü", "Suppe", "Mineralwasser", "Tiramisu"],
            ["Maria Tisch", "Paul Gast"]),
        Profile(VerticalProfileIds.Vet, "Tierarzt", RksvProductCategory.Other,
            ["Impfung", "Kastration", "Wurmkur", "Gesundheitscheck"],
            ["Familie Huber", "Tierheim Nord"]),
        Profile(VerticalProfileIds.Doctor, "Arzt", RksvProductCategory.Other,
            ["Ordination", "Blutabnahme", "Verband", "Beratung"],
            ["Eva Arzt", "Jonas Patient"]),
        Profile(VerticalProfileIds.HairSalon, "Friseur", RksvProductCategory.Other,
            ["Haarschnitt", "Färben", "Waschen", "Styling"],
            ["Sophie Schnitt", "Lara Farbe"]),
        Profile(VerticalProfileIds.MobileServices, "Mobile Dienste", RksvProductCategory.Other,
            ["Vor-Ort-Service", "Anfahrt", "Reparatur", "Inspektion"],
            ["Dienst Kunde", "Hausbesuch Eins"]),
        Profile(VerticalProfileIds.HandyShop, "Handyshop", RksvProductCategory.Other,
            ["Schutzfolie", "Ladekabel", "Hülle", "Displaytausch"],
            ["Handy Max", "IMEI Kunde"]),
        Profile(VerticalProfileIds.Taxi, "Taxi", RksvProductCategory.Other,
            ["Stadtfahrt", "Flughafenfahrt", "Nachtzuschlag", "Wartezeit"],
            ["Fahrgast Wien", "Nachtfahrt"]),
        Profile(VerticalProfileIds.TicketSales, "Ticketverkauf", RksvProductCategory.Other,
            ["Konzert A", "Konzert B", "Stehplatz", "VIP-Loge"],
            ["Konzertbesucher", "Abendkarte"]),
        Profile(VerticalProfileIds.Beherbergung, "Beherbergung", RksvProductCategory.Other,
            ["Zimmer Standard", "Zimmer Deluxe", "Frühstück", "Parkplatz"],
            ["Gast Zimmer", "Frühstück Gast"]),
        Profile(VerticalProfileIds.Retail, "Einzelhandel", RksvProductCategory.Other,
            ["T-Shirt", "Jeans", "Socken", "Gürtel"],
            ["Kunde Laden", "Stammkunde"]),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                row => row.Id == DemoTenantIds.Dev && row.Slug == "dev" && row.IsActive && row.DeletedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return;

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
        await EnsureProductsAsync(db, tenant.Id, taxGroupId, categories, now, cancellationToken).ConfigureAwait(false);
        await EnsureCustomersAsync(db, tenant.Id, now, cancellationToken).ConfigureAwait(false);
        await EnsureOpenRegisterAsync(db, tenant.Id, now, cancellationToken).ConfigureAwait(false);
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

        foreach (var profile in Catalog)
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
                CreatedBy = CreatedBy,
            };
            db.Categories.Add(category);
            byKey[profile.ProfileId] = category;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return byKey;
    }

    private static async Task EnsureProductsAsync(
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

        foreach (var profile in Catalog)
        {
            var category = categories[profile.ProfileId];
            for (var index = 0; index < profile.ProductNames.Count; index++)
            {
                var barcode = Barcode(profile.ProfileId, index + 1);
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
                    CreatedBy = CreatedBy,
                    UpdatedBy = CreatedBy,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureCustomersAsync(
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

        foreach (var profile in Catalog)
        {
            for (var index = 0; index < profile.CustomerNames.Count; index++)
            {
                var number = CustomerNumber(profile.ProfileId, index + 1);
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
                    CreatedBy = CreatedBy,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureOpenRegisterAsync(
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
                    RegisterNumber = RegisterNumber,
                    Location = "Hauptkasse",
                    StartingBalance = 0,
                    CurrentBalance = 0,
                    LastBalanceUpdate = now,
                    Status = RegisterStatus.Open,
                    IsDefaultForTenant = true,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = CreatedBy,
                };
                db.CashRegisters.Add(register);
            }
            else if (register.Status != RegisterStatus.Decommissioned)
            {
                register.Status = RegisterStatus.Open;
                register.LastBalanceUpdate = now;
                register.UpdatedAt = now;
                register.UpdatedBy = CreatedBy;
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
        if (hasOpenShift)
            return;

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
            CreatedBy = CreatedBy,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string Barcode(string profileId, int index) => $"DV-{profileId}-{index}";

    public static string CustomerNumber(string profileId, int index)
    {
        var compact = profileId.Replace("-", "", StringComparison.Ordinal);
        var stem = compact.Length <= 12 ? compact : compact[..12];
        return $"D-{stem}-{index}";
    }

    private static DevTenantProfileCatalog Profile(
        string profileId,
        string categoryName,
        RksvProductCategory fiscalCategory,
        string[] products,
        string[] customers) =>
        new(profileId, categoryName, fiscalCategory, products, customers);
}

public sealed record DevTenantProfileCatalog(
    string ProfileId,
    string CategoryName,
    RksvProductCategory FiscalCategory,
    IReadOnlyList<string> ProductNames,
    IReadOnlyList<string> CustomerNames);
