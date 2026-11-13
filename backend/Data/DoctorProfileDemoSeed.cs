using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data;

/// <summary>
/// Development tenant <c>dev-doctor</c> for the Arzt vertical profile.
/// Cashier <c>cashier-doctor</c> / <c>DevCashier1!</c>, register <c>KASSE-001</c>.
/// </summary>
public static class DoctorProfileDemoSeed
{
    public const string Slug = "dev-doctor";
    public const string CashierUserName = "cashier-doctor";
    public const string Password = "DevCashier1!";
    public static readonly Guid TenantId = Guid.Parse("b0000001-0001-4001-8001-00000000000d");
    public const string CashierUserId = "demo-cashier-doctor";

    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IHostEnvironment hostEnvironment,
        CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsDevelopment())
            return;

        var now = DateTime.UtcNow;
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(row => row.Slug == Slug, cancellationToken)
            .ConfigureAwait(false);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = TenantId,
                Name = "Arztpraxis",
                Slug = Slug,
                Email = "admin@dev-doctor.regkasse.at",
                Status = TenantStatuses.Active,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!await db.CompanySettings.IgnoreQueryFilters().AnyAsync(
                row => row.TenantId == tenant.Id,
                cancellationToken).ConfigureAwait(false))
        {
            db.CompanySettings.Add(new CompanySettings
            {
                TenantId = tenant.Id,
                VerticalProfileId = VerticalProfileIds.Doctor,
                CompanyName = "Arztpraxis Demo",
                CompanyAddress = "Wien 1",
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
            });
        }

        if (!await db.CashRegisters.IgnoreQueryFilters().AnyAsync(
                row => row.TenantId == tenant.Id,
                cancellationToken).ConfigureAwait(false))
        {
            db.CashRegisters.Add(new CashRegister
            {
                TenantId = tenant.Id,
                RegisterNumber = "KASSE-001",
                Location = "Hauptkasse",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = now,
                Status = RegisterStatus.Closed,
                IsDefaultForTenant = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (!await db.TaxGroups.IgnoreQueryFilters().AnyAsync(
                row => row.TenantId == tenant.Id,
                cancellationToken).ConfigureAwait(false))
        {
            db.TaxGroups.AddRange(TaxGroupSeedData.GetTaxGroups(tenant.Id));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!await db.Categories.IgnoreQueryFilters().AnyAsync(
                row => row.TenantId == tenant.Id && row.Key == "allgemein",
                cancellationToken).ConfigureAwait(false))
        {
            db.Categories.Add(new Category
            {
                TenantId = tenant.Id,
                Key = "allgemein",
                Name = "Allgemein",
                VatRate = 20m,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!await db.Products.IgnoreQueryFilters().AnyAsync(
                row => row.TenantId == tenant.Id,
                cancellationToken).ConfigureAwait(false))
        {
            var category = await db.Categories.IgnoreQueryFilters()
                .FirstAsync(row => row.TenantId == tenant.Id && row.Key == "allgemein", cancellationToken)
                .ConfigureAwait(false);
            var taxGroup = await db.TaxGroups.IgnoreQueryFilters()
                .FirstAsync(row => row.TenantId == tenant.Id && row.IsDefault, cancellationToken)
                .ConfigureAwait(false);
            db.Products.Add(new Product
            {
                TenantId = tenant.Id,
                Name = "Demo Produkt 1",
                Description = "Arztpraxis Demo",
                Price = 9.99m,
                TaxType = TaxTypes.Standard,
                TaxRate = 20m,
                Category = category.Name,
                CategoryId = category.Id,
                TaxGroupId = taxGroup.Id,
                IsActive = true,
                StockQuantity = 100,
                MinStockLevel = 0,
                Unit = "Stk",
                Cost = 0,
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsSellableAddOn = false,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = "doctor-demo-seed",
                UpdatedBy = "doctor-demo-seed",
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var user = await userManager.FindByNameAsync(CashierUserName).ConfigureAwait(false);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = CashierUserId,
                UserName = CashierUserName,
                Email = "cashier-doctor@dev-doctor.regkasse.at",
                FirstName = "Arzt",
                LastName = "Kasse",
                EmployeeNumber = "DEMO-DOCTOR-001",
                Role = Roles.Cashier,
                TaxNumber = "ATU10000013",
                Notes = "Development cashier for dev-doctor",
                IsActive = true,
                EmailConfirmed = true,
                AccountType = "Staff",
                IsDemo = false,
                MustChangePasswordOnNextLogin = false,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var created = await userManager.CreateAsync(user, Password).ConfigureAwait(false);
            if (!created.Succeeded)
            {
                Console.WriteLine(
                    "dev-doctor cashier create failed: {0}",
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, Roles.Cashier).ConfigureAwait(false);
        }

        await membershipProvisioner
            .ProvisionActiveMembershipAsync(user.Id, tenant.Id, isOwner: false, cancellationToken)
            .ConfigureAwait(false);
    }
}
