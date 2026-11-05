using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
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
            ["dev"] = VerticalProfileIds.GastronomyTables,
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
                && (row.Slug == "dev" || row.Slug.StartsWith("dev-")))
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

            if (!string.Equals(tenant.Slug, "dev", StringComparison.Ordinal))
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
            .Where(row => row.Id == DemoTenantIds.Dev && row.Slug == "dev" && row.DeletedAtUtc == null)
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
            .FirstOrDefaultAsync(row => row.Slug == "dev" && row.IsActive, cancellationToken)
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
    /// Demo POS staff with a stable username, matching email, and the mandant they belong to.
    /// <c>cashier1</c> is Cashier on <c>dev</c>. <c>manager1</c> is Manager on <c>prod</c>.
    /// </summary>
    public static async Task SeedPosDemoStaffAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        CancellationToken cancellationToken = default)
    {
        await EnsureDemoStaffUserAsync(
            db,
            userManager,
            membershipProvisioner,
            userName: "cashier1",
            userId: "demo-cashier-dev",
            tenantSlug: "dev",
            role: Roles.Cashier,
            firstName: "Kasse",
            lastName: "Dev",
            employeeNumber: "DEMO-CASHIER-001",
            cancellationToken).ConfigureAwait(false);

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
        if (tenant.Id == DemoTenantIds.Dev || string.Equals(tenant.Slug, "dev", StringComparison.Ordinal))
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

public readonly record struct DevTenantSeedReport(
    int ForeignCategoriesRemoved,
    int ForeignProductsRemoved,
    int GenericCategoriesRemoved,
    int GenericProductsRemoved,
    int TenantsSeeded,
    int PaymentProtectedProducts);
