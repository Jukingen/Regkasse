using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using TenantSlugs = KasseAPI_Final.Constants.TenantSlugs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data.Seed;

/// <summary>
/// Development catalog. Each <c>dev</c> / <c>dev-*</c> tenant receives only its own vertical
/// profile (slug <c>dev</c> is gastronomy-tables). Wrong demo categories are deleted unless a
/// payment still references the product. Payments, receipts, and TSE signatures are never written.
/// </summary>
public static class DevTenantSeeder
{
    public const string RegisterNumber = "KASSE-001";
    public const string CreatedBy = "dev-tenant-seed";
    public const string KitchenUserName = "chef1";
    public const string KitchenPassword = "DevCashier1!";
    public const string KitchenUserId = "demo-chef-dev";

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

    /// <summary>Slug → profile for Development fixtures. <c>dev</c> is gastronomy-tables.</summary>
    private static readonly IReadOnlyDictionary<string, string> SlugProfiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TenantSlugs.Dev] = VerticalProfileIds.GastronomyTables,
            ["dev-gastronomy-tables"] = VerticalProfileIds.GastronomyTables,
            ["dev-vet"] = VerticalProfileIds.Vet,
            ["dev-doctor"] = VerticalProfileIds.Doctor,
            ["dev-hair-salon"] = VerticalProfileIds.HairSalon,
            ["dev-mobile-services"] = VerticalProfileIds.MobileServices,
            ["dev-handy-shop"] = VerticalProfileIds.HandyShop,
            ["dev-taxi"] = VerticalProfileIds.Taxi,
            ["dev-ticket-sales"] = VerticalProfileIds.TicketSales,
            ["dev-beherbergung"] = VerticalProfileIds.Beherbergung,
            ["dev-retail"] = VerticalProfileIds.Retail,
        };

    public const string GenericCategoryKey = "allgemein";

    public static async Task<DevTenantSeedReport> SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var (foreignCategories, foreignProducts, foreignProtected) = await RemoveForeignCatalogOnDevAsync(db, cancellationToken)
            .ConfigureAwait(false);

        var tenants = await db.Tenants
            .IgnoreQueryFilters()
            .Where(row => row.IsActive && row.DeletedAtUtc == null && row.Slug != null
                && (row.Slug == TenantSlugs.Dev || row.Slug.StartsWith("dev-")))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var genericCategories = 0;
        var genericProducts = 0;
        var genericProtected = 0;
        var tenantsSeeded = 0;
        foreach (var tenant in tenants)
        {
            var profileId = await ResolveDevTenantProfileAsync(db, tenant, cancellationToken).ConfigureAwait(false);
            if (profileId is null)
                continue;

            if (!string.Equals(tenant.Slug, TenantSlugs.Dev, StringComparison.Ordinal))
            {
                var (categories, products, protectedProducts) = await RemoveGenericAllgemeinAsync(db, tenant.Id, cancellationToken)
                    .ConfigureAwait(false);
                genericCategories += categories;
                genericProducts += products;
                genericProtected += protectedProducts;
            }

            await TenantDemoSeeder.SeedTenantAsync(db, tenant.Id, profileId, cancellationToken).ConfigureAwait(false);
            await PersistVerticalProfileAsync(db, tenant, profileId, cancellationToken).ConfigureAwait(false);
            tenantsSeeded++;
        }

        return new DevTenantSeedReport(
            foreignCategories,
            foreignProducts,
            genericCategories,
            genericProducts,
            tenantsSeeded,
            foreignProtected + genericProtected);
    }

    /// <summary>
    /// On slug <c>dev</c>, delete demo rows from the old all-vertical seed.
    /// A product named in <c>payment_details</c> is deactivated instead. Fiscal tables are not updated.
    /// </summary>
    private static async Task<(int Categories, int Products, int Protected)> RemoveForeignCatalogOnDevAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var devId = await db.Tenants
            .IgnoreQueryFilters()
            .Where(row => row.Id == DemoTenantIds.Dev && row.Slug == TenantSlugs.Dev && row.DeletedAtUtc == null)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (devId is null)
            return (0, 0, 0);

        var categoryKeys = await db.Categories
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == devId)
            .Select(row => new { row.Id, row.Key })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var keyById = categoryKeys.ToDictionary(row => row.Id, row => row.Key);

        var categories = await db.Categories
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == devId
                && row.CreatedBy == CreatedBy
                && row.Key != VerticalProfileIds.GastronomyTables)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var products = await db.Products
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == devId && row.CreatedBy == CreatedBy)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        products = products
            .Where(row => keyById.TryGetValue(row.CategoryId, out var key)
                && !string.Equals(key, VerticalProfileIds.GastronomyTables, StringComparison.Ordinal))
            .ToList();

        return await RemoveCatalogRowsAsync(db, categories, products, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete the generic <c>allgemein</c> demo category on a vertical fixture tenant.
    /// The tenant then keeps only the catalog for its own profile. Payment-referenced products stay inactive.
    /// </summary>
    private static async Task<(int Categories, int Products, int Protected)> RemoveGenericAllgemeinAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.Key == GenericCategoryKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (categories.Count == 0)
            return (0, 0, 0);

        var categoryIds = categories.Select(row => row.Id).ToHashSet();
        var products = await db.Products
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && categoryIds.Contains(row.CategoryId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await RemoveCatalogRowsAsync(db, categories, products, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(int Categories, int Products, int Protected)> RemoveCatalogRowsAsync(
        AppDbContext db,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        if (categories.Count == 0 && products.Count == 0)
            return (0, 0, 0);

        var protectedIds = await FindPaymentReferencedProductIdsAsync(db, products, cancellationToken)
            .ConfigureAwait(false);
        var toDelete = products.Where(row => !protectedIds.Contains(row.Id)).ToList();
        var toKeep = products.Where(row => protectedIds.Contains(row.Id)).ToList();
        var now = DateTime.UtcNow;

        if (toDelete.Count > 0 && db.Database.IsRelational())
            await DeleteNonFiscalProductDependentsAsync(db, toDelete.Select(row => row.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);

        if (toDelete.Count > 0)
            db.Products.RemoveRange(toDelete);

        foreach (var product in toKeep)
        {
            product.IsActive = false;
            product.UpdatedAt = now;
            product.UpdatedBy = CreatedBy;
        }

        if (toDelete.Count > 0 || toKeep.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var categoryIds = categories.Select(row => row.Id).ToList();
        var occupied = await db.Products
            .IgnoreQueryFilters()
            .Where(row => categoryIds.Contains(row.CategoryId))
            .Select(row => row.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var occupiedIds = occupied.ToHashSet();
        var removedCategories = 0;
        foreach (var category in categories)
        {
            if (occupiedIds.Contains(category.Id))
            {
                category.IsActive = false;
                category.UpdatedAt = now;
                category.UpdatedBy = CreatedBy;
                continue;
            }

            db.Categories.Remove(category);
            removedCategories++;
        }

        if (categories.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (removedCategories, toDelete.Count, toKeep.Count);
    }

    private static async Task<HashSet<Guid>> FindPaymentReferencedProductIdsAsync(
        AppDbContext db,
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        var wanted = products.Select(row => row.Id).ToHashSet();
        if (wanted.Count == 0)
            return wanted;

        var tenantIds = products.Select(row => row.TenantId).Distinct().ToList();
        var registerIds = await db.CashRegisters
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(row => tenantIds.Contains(row.TenantId))
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (registerIds.Count == 0)
            return [];

        var documents = await db.PaymentDetails
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(row => registerIds.Contains(row.CashRegisterId))
            .Select(row => row.PaymentItems)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hit = new HashSet<Guid>();
        foreach (var document in documents)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                if (!TryReadGuid(item, "productId", out var productId)
                    && !TryReadGuid(item, "ProductId", out productId))
                    continue;
                if (wanted.Contains(productId))
                    hit.Add(productId);
            }
        }

        return hit;
    }

    private static bool TryReadGuid(JsonElement item, string propertyName, out Guid productId)
    {
        productId = default;
        if (!item.TryGetProperty(propertyName, out var property))
            return false;
        if (property.ValueKind == JsonValueKind.String && Guid.TryParse(property.GetString(), out productId))
            return true;
        return property.TryGetGuid(out productId);
    }

    /// <summary>
    /// Clears RESTRICT children of products about to be deleted. Does not query or update
    /// payment_details, receipts, or TSE signatures.
    /// </summary>
    private static async Task DeleteNonFiscalProductDependentsAsync(
        AppDbContext db,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken)
    {
        await db.ProductImeis.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.ProductPriceHistories.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.TaxHistories.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.CartItems.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.SplitItems.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.ProductVariants.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.RoomMinibarItems.IgnoreQueryFilters()
            .Where(row => row.ProductId != null && productIds.Contains(row.ProductId.Value))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.OnlineOrderProductMappings.IgnoreQueryFilters()
            .Where(row => productIds.Contains(row.ProductId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var aliases = await db.Products
            .IgnoreQueryFilters()
            .Where(row => row.OriginalProductId != null && productIds.Contains(row.OriginalProductId.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var alias in aliases)
            alias.OriginalProductId = null;

        var feeLocations = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .Where(row =>
                (row.DeliveryFeeProductId != null && productIds.Contains(row.DeliveryFeeProductId.Value))
                || (row.ServiceFeeProductId != null && productIds.Contains(row.ServiceFeeProductId.Value)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var location in feeLocations)
        {
            if (location.DeliveryFeeProductId is Guid delivery && productIds.Contains(delivery))
                location.DeliveryFeeProductId = null;
            if (location.ServiceFeeProductId is Guid service && productIds.Contains(service))
                location.ServiceFeeProductId = null;
        }

        if (aliases.Count > 0 || feeLocations.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes <c>company_settings.vertical_profile_id</c> so POS <c>GET /pos/vertical-profile</c>
    /// matches the catalog just seeded. Existing rows are updated in place.
    /// </summary>
    private static async Task PersistVerticalProfileAsync(
        AppDbContext db,
        Tenant tenant,
        string profileId,
        CancellationToken cancellationToken)
    {
        var settings = await db.CompanySettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Id, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
        {
            db.CompanySettings.Add(new CompanySettings
            {
                TenantId = tenant.Id,
                VerticalProfileId = profileId,
                CompanyName = string.IsNullOrWhiteSpace(tenant.Name) ? tenant.Slug : tenant.Name,
                CompanyAddress = "Wien",
                CompanyTaxNumber = "ATU12345678",
                BusinessHours = new Dictionary<string, string>(),
                Currency = "EUR",
                Country = "AT",
                Language = "de-DE",
                TimeZone = "Europe/Vienna",
                DateFormat = "dd.MM.yyyy",
                TimeFormat = "HH:mm",
                TaxCalculationMethod = "Standard",
                InvoiceNumbering = "Sequential",
                ReceiptNumbering = "Sequential",
                DefaultPaymentMethod = "Cash",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = CreatedBy,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (string.Equals(settings.VerticalProfileId, profileId, StringComparison.Ordinal))
            return;

        settings.VerticalProfileId = profileId;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = CreatedBy;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Development cashier for the kitchen display on tenant slug <c>dev</c>.
    /// Username <c>chef1</c>, password <c>DevCashier1!</c>, role <see cref="Roles.Kitchen"/>.
    /// </summary>
    public static async Task SeedKitchenDemoUserAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.Slug == TenantSlugs.Dev && row.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return;

        var now = DateTime.UtcNow;
        var user = await userManager.FindByNameAsync(KitchenUserName).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = KitchenUserId,
                UserName = KitchenUserName,
                Email = "chef1@dev.regkasse.at",
                FirstName = "Küche",
                LastName = "Dev",
                EmployeeNumber = "DEMO-CHEF-001",
                Role = Roles.Kitchen,
                Notes = "Development kitchen display user for tenant dev",
                IsActive = true,
                EmailConfirmed = true,
                AccountType = "Staff",
                IsDemo = false,
                MustChangePasswordOnNextLogin = false,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var created = await userManager.CreateAsync(user, KitchenPassword).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                Console.WriteLine(
                    "dev kitchen user create failed: {0}",
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return;
            }
        }

        if (!string.Equals(user.Role, Roles.Kitchen, StringComparison.Ordinal))
        {
            user.Role = Roles.Kitchen;
            user.IsActive = true;
            user.MustChangePasswordOnNextLogin = false;
            user.UpdatedAt = now;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Kitchen).ConfigureAwait(false))
            await userManager.AddToRoleAsync(user, Roles.Kitchen).ConfigureAwait(false);

        await membershipProvisioner
            .ProvisionActiveMembershipAsync(user.Id, tenant.Id, isOwner: false, cancellationToken)
            .ConfigureAwait(false);

        var registerId = await db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenant.Id && row.IsActive)
            .OrderByDescending(row => row.IsDefaultForTenant)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (registerId is not Guid assignedRegister || assignedRegister == Guid.Empty)
            return;

        var registerText = assignedRegister.ToString();
        var settings = await db.UserSettings
            .FirstOrDefaultAsync(row => row.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);
        if (settings is null)
        {
            db.UserSettings.Add(new UserSettings
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CashRegisterId = registerText,
                Language = "de-DE",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else if (string.IsNullOrWhiteSpace(settings.CashRegisterId))
        {
            settings.CashRegisterId = registerText;
            settings.UpdatedAt = now;
        }

        if (user.CashRegisterId != assignedRegister)
        {
            user.CashRegisterId = assignedRegister;
            user.UpdatedAt = now;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Demo cashiers used by POS (manual login and <c>frontend/e2e/fixtures/test-users.json</c>).
    /// <see cref="IsPrimary"/> receives the tenant's default register in <see cref="AssignDemoCashRegistersAsync"/>.
    /// </summary>
    public static readonly DemoCashierStaffSpec[] DemoCashierStaff =
    [
        new(TenantSlugs.Dev, "cashier1", "demo-cashier-dev", "Kasse", "Dev", "DEMO-CASHIER-001", IsPrimary: true),
        new(TenantSlugs.Dev, "cashier-gastro", "demo-cashier-gastro", "Cashier", "Gastro", "DEMO-GASTRO-001", IsPrimary: false),
        new("dev-gastronomy-tables", "cashier-tables", "demo-cashier-tables", "Cashier", "Tables", "DEMO-TABLES-001", IsPrimary: true),
        new("dev-vet", "cashier-vet", "demo-cashier-vet", "Tierarzt", "Kasse", "DEMO-VET-001", IsPrimary: true),
        new("dev-doctor", "cashier-doctor", "demo-cashier-doctor", "Arzt", "Kasse", "DEMO-DOCTOR-001", IsPrimary: true),
        new("dev-hair-salon", "cashier-hair", "demo-cashier-hair", "Friseur", "Kasse", "DEMO-HAIR-001", IsPrimary: true),
        new("dev-mobile-services", "cashier-mobile", "demo-cashier-mobile", "Mobil", "Kasse", "DEMO-MOBILE-001", IsPrimary: true),
        new("dev-handy-shop", "cashier-handy", "demo-cashier-handy", "Handy", "Kasse", "DEMO-HANDY-001", IsPrimary: true),
        new("dev-taxi", "cashier-taxi", "demo-cashier-taxi", "Taxi", "Kasse", "DEMO-TAXI-001", IsPrimary: true),
        new("dev-ticket-sales", "cashier-ticket", "demo-cashier-ticket", "Ticket", "Kasse", "DEMO-TICKET-001", IsPrimary: true),
        new("dev-beherbergung", "cashier-lodge", "demo-cashier-lodge", "Lodge", "Kasse", "DEMO-LODGE-001", IsPrimary: true),
    ];

    /// <summary>
    /// Demo POS staff with a stable username, matching email, and the mandant they belong to.
    /// <c>cashier1</c> is Cashier on <c>dev</c>. <c>manager1</c> is Manager on <c>prod</c>.
    /// Vertical cashiers match the POS E2E fixture usernames. Password is <see cref="KitchenPassword"/>.
    /// </summary>
    public static async Task SeedPosDemoStaffAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        CancellationToken cancellationToken = default)
    {
        foreach (var spec in DemoCashierStaff)
        {
            await EnsureDemoStaffUserAsync(
                db,
                userManager,
                membershipProvisioner,
                userName: spec.UserName,
                userId: spec.UserId,
                tenantSlug: spec.TenantSlug,
                role: Roles.Cashier,
                firstName: spec.FirstName,
                lastName: spec.LastName,
                employeeNumber: spec.EmployeeNumber,
                cancellationToken).ConfigureAwait(false);
        }

        await EnsureDemoStaffUserAsync(
            db,
            userManager,
            membershipProvisioner,
            userName: "manager1",
            userId: "demo-manager-prod",
            tenantSlug: "prod",
            role: Roles.Manager,
            firstName: "Leitung",
            lastName: "Prod",
            employeeNumber: "DEMO-MANAGER-001",
            cancellationToken).ConfigureAwait(false);

        await EnsureDemoStaffUserAsync(
            db,
            userManager,
            membershipProvisioner,
            userName: "admin1",
            userId: "demo-admin1-dev",
            tenantSlug: TenantSlugs.Dev,
            role: Roles.Manager,
            firstName: "Admin",
            lastName: "Dev",
            employeeNumber: "DEMO-ADMIN-001",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets <see cref="CashRegister.AssignedUserId"/> so POS can list a register for each demo cashier.
    /// The picker only returns rows assigned to the signed-in user. One register goes to the primary
    /// cashier (<c>KASSE-001</c> → <c>cashier1</c> on <c>dev</c>). Further demo cashiers on the same
    /// tenant each receive a different operational register, created closed when none is free.
    /// A single cashier receives every still-unassigned operational register. Assignments that already
    /// point at someone outside this catalog are left unchanged.
    /// </summary>
    public static async Task AssignDemoCashRegistersAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        foreach (var group in DemoCashierStaff.GroupBy(row => row.TenantSlug, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    row => row.Slug == group.Key && row.IsActive && row.DeletedAtUtc == null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (tenant is null)
                continue;

            var cashiers = new List<ApplicationUser>();
            foreach (var spec in group
                         .OrderByDescending(row => row.IsPrimary)
                         .ThenBy(row => row.UserName, StringComparer.Ordinal))
            {
                var user = await db.Users
                    .FirstOrDefaultAsync(row => row.UserName == spec.UserName, cancellationToken)
                    .ConfigureAwait(false);
                if (user is null || !user.IsActive)
                    continue;

                var member = await db.UserTenantMemberships
                    .IgnoreQueryFilters()
                    .AnyAsync(
                        row => row.UserId == user.Id && row.TenantId == tenant.Id && row.IsActive,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!member)
                    continue;

                cashiers.Add(user);
            }

            if (cashiers.Count == 0)
                continue;

            var registers = await db.CashRegisters
                .IgnoreQueryFilters()
                .Where(row => row.TenantId == tenant.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var changed = false;
            foreach (var cashier in cashiers)
            {
                if (HasOperationalAssignment(registers, cashier.Id))
                    continue;

                var free = registers
                    .Where(IsOperationalRegister)
                    .Where(row => string.IsNullOrEmpty(row.AssignedUserId))
                    .OrderByDescending(row => row.IsDefaultForTenant)
                    .ThenBy(row => row.RegisterNumber, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (free is null)
                {
                    free = CreateClosedDemoRegister(tenant, registers, now);
                    registers.Add(free);
                    db.CashRegisters.Add(free);
                }

                free.AssignedUserId = cashier.Id;
                free.UpdatedAt = now;
                free.UpdatedBy = CreatedBy;
                changed = true;
            }

            if (cashiers.Count == 1)
            {
                foreach (var register in registers.Where(IsOperationalRegister))
                {
                    if (!string.IsNullOrEmpty(register.AssignedUserId))
                        continue;

                    register.AssignedUserId = cashiers[0].Id;
                    register.UpdatedAt = now;
                    register.UpdatedBy = CreatedBy;
                    changed = true;
                }
            }

            if (changed)
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await EnsureDemoStartbelegMarkersAsync(db, cancellationToken).ConfigureAwait(false);
        await EnsureDemoPaymentMethodsAsync(db, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks operational <c>dev</c> / <c>dev-*</c> registers as commissioned.
    /// POS refuses to open a shift until <see cref="CashRegister.StartbelegCreatedAt"/> is set
    /// (<c>REGISTER_STARTBELEG_REQUIRED</c>). This is a Development marker only: it does not
    /// create a signed Sonderbeleg and does not run the signature pipeline.
    /// </summary>
    public static async Task EnsureDemoStartbelegMarkersAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants
            .IgnoreQueryFilters()
            .Where(row => row.IsActive && row.DeletedAtUtc == null && row.Slug != null
                && (row.Slug == TenantSlugs.Dev || row.Slug.StartsWith("dev-")))
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantIds.Count == 0)
            return;

        var registers = await db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => tenantIds.Contains(row.TenantId) && row.StartbelegCreatedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var changed = false;
        foreach (var register in registers)
        {
            if (!IsOperationalRegister(register))
                continue;

            register.StartbelegCreatedAt = now;
            register.UpdatedAt = now;
            register.UpdatedBy = CreatedBy;
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// POS lists payment methods from <c>payment_method_definitions</c> for the open register.
    /// Registers created by the demo seed have none until this runs.
    /// </summary>
    public static async Task EnsureDemoPaymentMethodsAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants
            .IgnoreQueryFilters()
            .Where(row => row.IsActive && row.DeletedAtUtc == null && row.Slug != null
                && (row.Slug == TenantSlugs.Dev || row.Slug.StartsWith("dev-")))
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantIds.Count == 0)
            return;

        var registers = await db.CashRegisters
            .IgnoreQueryFilters()
            .Where(row => tenantIds.Contains(row.TenantId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bootstrap = new PaymentMethodDefinitionBootstrapService(db);
        foreach (var register in registers)
        {
            if (!IsOperationalRegister(register))
                continue;

            await bootstrap
                .EnsureDefaultsForCashRegisterAsync(register.TenantId, register.Id, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsOperationalRegister(CashRegister register) =>
        register.IsActive
        && register.DecommissionedAtUtc == null
        && register.Status is RegisterStatus.Closed or RegisterStatus.Open;

    private static bool HasOperationalAssignment(IReadOnlyList<CashRegister> registers, string userId) =>
        registers.Any(row => IsOperationalRegister(row)
            && string.Equals(row.AssignedUserId, userId, StringComparison.Ordinal));

    private static CashRegister CreateClosedDemoRegister(Tenant tenant, IReadOnlyList<CashRegister> existing, DateTime now)
    {
        var taken = existing
            .Select(row => row.RegisterNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = CashRegisterNumbers.ForTenant(tenant.Id, tenant.Slug);
        if (taken.Contains(number))
        {
            number = string.Empty;
            for (var index = 2; index < 100; index++)
            {
                var candidate = $"KASSE-{index:000}";
                if (taken.Contains(candidate))
                    continue;
                number = candidate;
                break;
            }
        }

        if (string.IsNullOrEmpty(number))
            number = "K-" + Guid.NewGuid().ToString("N")[..18].ToUpperInvariant();

        var hasDefault = existing.Any(row => row.IsActive && row.IsDefaultForTenant);
        return new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            RegisterNumber = number,
            Location = "Hauptkasse",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            IsDefaultForTenant = !hasDefault,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = CreatedBy,
            UpdatedBy = CreatedBy,
        };
    }

    /// <summary>
    /// Usernames that stay active on the Development tenant. Other demo memberships there are deactivated.
    /// </summary>
    public static readonly string[] KeptDevelopmentUserNames =
    [
        "cashier1",
        "chef1",
        "manager1",
        "admin1",
        "cashier-gastro",
        "cashier-tables",
    ];

    /// <summary>
    /// Deactivates Development memberships that are not in <see cref="KeptDevelopmentUserNames"/>.
    /// Super Admin accounts are left active. Users with no other mandant are marked inactive.
    /// </summary>
    public static async Task PruneDevelopmentDemoUsersAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        var dev = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.Slug == TenantSlugs.Dev && row.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (dev is null)
            return;

        var keep = new HashSet<string>(KeptDevelopmentUserNames, StringComparer.OrdinalIgnoreCase);
        var memberships = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == dev.Id && row.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var membership in memberships)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(membership.UserId).ConfigureAwait(false);
            if (user is null)
                continue;

            var userName = user.UserName?.Trim() ?? string.Empty;
            if (keep.Contains(userName)
                || string.Equals(user.Id, "demo-tenant-admin-dev", StringComparison.OrdinalIgnoreCase)
                || string.Equals(user.Role, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                if (!user.IsActive)
                {
                    user.IsActive = true;
                    user.UpdatedAt = DateTime.UtcNow;
                    await userManager.UpdateAsync(user).ConfigureAwait(false);
                }

                continue;
            }

            membership.IsActive = false;
            var hasOtherTenant = await db.UserTenantMemberships
                .IgnoreQueryFilters()
                .AnyAsync(
                    row => row.UserId == user.Id && row.IsActive && row.TenantId != dev.Id,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!hasOtherTenant && user.IsActive)
            {
                user.IsActive = false;
                user.UpdatedAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user).ConfigureAwait(false);
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns <c>username@slug.regkasse.at</c> when the stored address is empty or is another
    /// <c>*.regkasse.at</c> address that does not match the username and primary mandant.
    /// Usernames that are already email addresses, and addresses on other hosts, are left alone.
    /// </summary>
    public static string? CorrectedDemoUserEmail(string? userName, string? email, string? tenantSlug)
    {
        var name = userName?.Trim();
        var slug = tenantSlug?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(name) || name.Contains('@') || string.IsNullOrEmpty(slug))
            return null;

        var expected = $"{name}@{slug}.regkasse.at";
        if (string.IsNullOrWhiteSpace(email))
            return expected;

        var at = email.IndexOf('@');
        if (at <= 0)
            return expected;

        if (email.Equals(expected, StringComparison.OrdinalIgnoreCase))
            return null;

        var host = email[(at + 1)..];
        if (host.Equals("regkasse.at", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".regkasse.at", StringComparison.OrdinalIgnoreCase))
            return expected;

        return null;
    }

    /// <summary>
    /// Rewrites demo emails whose local part does not match the username on the user's primary mandant.
    /// </summary>
    public static async Task AlignDemoUserEmailsAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        var memberships = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.IsActive)
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking().Where(tenant =>
                    tenant.IsActive && tenant.Slug != SystemTenantIds.PlatformSlug),
                row => row.TenantId,
                tenant => tenant.Id,
                (row, tenant) => new
                {
                    row.UserId,
                    row.IsOwner,
                    row.CreatedAtUtc,
                    tenant.Slug,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var primaryByUser = memberships
            .GroupBy(row => row.UserId)
            .Select(group => group
                .OrderByDescending(row => row.IsOwner)
                .ThenBy(row => row.CreatedAtUtc)
                .First());

        foreach (var membership in primaryByUser)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(membership.UserId).ConfigureAwait(false);
            if (user is null)
                continue;

            var corrected = CorrectedDemoUserEmail(user.UserName, user.Email, membership.Slug);
            if (corrected is null || string.Equals(user.Email, corrected, StringComparison.OrdinalIgnoreCase))
                continue;

            var taken = await userManager.FindByEmailAsync(corrected).ConfigureAwait(false);
            if (taken != null && taken.Id != user.Id)
                continue;

            var result = await userManager.SetEmailAsync(user, corrected).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                Console.WriteLine(
                    "demo user email align failed for {0}: {1}",
                    user.UserName,
                    string.Join("; ", result.Errors.Select(error => error.Description)));
            }
        }
    }

    private static async Task EnsureDemoStaffUserAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        string userName,
        string userId,
        string tenantSlug,
        string role,
        string firstName,
        string lastName,
        string employeeNumber,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.Slug == tenantSlug && row.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
            return;

        var email = $"{userName}@{tenantSlug}.regkasse.at";
        var now = DateTime.UtcNow;
        var user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = userId,
                UserName = userName,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmployeeNumber = employeeNumber,
                Role = role,
                Notes = $"Development {role} user for tenant {tenantSlug}",
                IsActive = true,
                EmailConfirmed = true,
                AccountType = "Staff",
                IsDemo = false,
                MustChangePasswordOnNextLogin = false,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var created = await userManager.CreateAsync(user, KitchenPassword).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                Console.WriteLine(
                    "demo staff user create failed for {0}: {1}",
                    userName,
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return;
            }
        }

        var corrected = CorrectedDemoUserEmail(user.UserName, user.Email, tenantSlug);
        if (corrected != null)
        {
            var taken = await userManager.FindByEmailAsync(corrected).ConfigureAwait(false);
            if (taken == null || taken.Id == user.Id)
                await userManager.SetEmailAsync(user, corrected).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            user.Role = role;
            user.UpdatedAt = now;
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }

        if (!await userManager.IsInRoleAsync(user, user.Role ?? role).ConfigureAwait(false))
            await userManager.AddToRoleAsync(user, user.Role ?? role).ConfigureAwait(false);

        await membershipProvisioner
            .ProvisionActiveMembershipAsync(user.Id, tenant.Id, isOwner: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string?> ResolveDevTenantProfileAsync(
        AppDbContext db,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        if (tenant.Id == DemoTenantIds.Dev || string.Equals(tenant.Slug, TenantSlugs.Dev, StringComparison.Ordinal))
            return VerticalProfileIds.GastronomyTables;

        var stored = await db.CompanySettings
            .IgnoreQueryFilters()
            .Where(row => row.TenantId == tenant.Id)
            .Select(row => row.VerticalProfileId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (Catalog.Any(row => row.ProfileId == stored))
            return stored;

        return SlugProfiles.TryGetValue(tenant.Slug, out var mapped) ? mapped : null;
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

public readonly record struct DemoCashierStaffSpec(
    string TenantSlug,
    string UserName,
    string UserId,
    string FirstName,
    string LastName,
    string EmployeeNumber,
    bool IsPrimary);

public readonly record struct DevTenantSeedReport(
    int ForeignCategoriesRemoved,
    int ForeignProductsRemoved,
    int GenericCategoriesRemoved,
    int GenericProductsRemoved,
    int TenantsSeeded,
    int PaymentProtectedProducts);
