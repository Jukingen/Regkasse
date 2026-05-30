using System.Security.Cryptography;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private const string DefaultRegisterNumber = "KASSE-001";
    private const string DefaultRegisterLocation = "Hauptkasse";
    private const string DefaultCategoryName = "Allgemein";

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantMembershipProvisioner _membershipProvisioner;
    private readonly IUserUniquenessValidationService _uniquenessValidation;
    private readonly IDemoProductImportService _demoProductImport;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IUserUniquenessValidationService uniquenessValidation,
        IDemoProductImportService demoProductImport,
        ILogger<TenantProvisioningService> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _uniquenessValidation = uniquenessValidation;
        _demoProductImport = demoProductImport;
        _logger = logger;
    }

    public async Task<(TenantProvisioningResult? Result, string? Error)> ProvisionAsync(
        Tenant tenant,
        string? adminEmail,
        string? adminPassword,
        bool grantTrialLicense,
        bool importDemoMenu = false,
        CancellationToken cancellationToken = default)
    {
        var resolvedEmail = ResolveAdminEmail(tenant, adminEmail);
        if (await _uniquenessValidation.IsEmailTakenByOtherUserAsync(resolvedEmail, excludeUserId: null)
                .ConfigureAwait(false))
        {
            return (null, $"Admin email '{resolvedEmail}' is already in use.");
        }

        var existingUser = await _userManager.FindByEmailAsync(resolvedEmail).ConfigureAwait(false);
        if (existingUser != null)
            return (null, $"Admin email '{resolvedEmail}' is already in use.");

        var password = string.IsNullOrWhiteSpace(adminPassword)
            ? GenerateCompliantPassword()
            : adminPassword.Trim();

        if (password.Length < 8)
            return (null, "Admin password must be at least 8 characters.");

        var now = DateTime.UtcNow;
        var cashRegister = new CashRegister
        {
            TenantId = tenant.Id,
            RegisterNumber = DefaultRegisterNumber,
            Location = DefaultRegisterLocation,
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            CreatedAt = now,
            IsActive = true,
        };
        _db.CashRegisters.Add(cashRegister);

        Category category;
        IReadOnlyList<Guid> productIds;

        if (importDemoMenu)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var importResult = await _demoProductImport
                .ImportDemoProductsAsync(tenant.Id, new DemoImportRequest(), progress: null, cancellationToken)
                .ConfigureAwait(false);

            if (!importResult.Success)
            {
                return (null, importResult.ErrorMessage ?? "Demo menu import failed.");
            }

            category = await _db.Categories
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenant.Id)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .FirstAsync(cancellationToken)
                .ConfigureAwait(false);

            productIds = importResult.ProductIds;
        }
        else
        {
            category = new Category
            {
                TenantId = tenant.Id,
                Key = CategoryKey.FromDisplayName(DefaultCategoryName),
                Name = DefaultCategoryName,
                VatRate = 20m,
                SortOrder = 0,
                FiscalCategory = RksvProductCategory.Food,
                IsSystemCategory = true,
                CreatedAt = now,
                IsActive = true,
            };
            _db.Categories.Add(category);

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var products = CreateDemoProducts(tenant.Id, category);
            _db.Products.AddRange(products);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            productIds = products.Select(p => p.Id).ToList();
        }

        var adminUser = new ApplicationUser
        {
            UserName = resolvedEmail,
            Email = resolvedEmail,
            FirstName = "Admin",
            LastName = tenant.Name.Length > 50 ? tenant.Name[..50] : tenant.Name,
            EmployeeNumber = "ADMIN001",
            Role = Roles.Manager,
            Notes = $"Provisioned admin for tenant {tenant.Slug}",
            IsActive = true,
            EmailConfirmed = true,
            AccountType = "Admin",
            IsDemo = false,
            CreatedAt = now,
            UpdatedAt = now,
            CashRegisterId = cashRegister.Id,
        };

        adminUser.MustChangePasswordOnNextLogin = true;

        var createResult = await _userManager.CreateAsync(adminUser, password).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            var detail = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning(
                "Tenant provisioning failed for {TenantId}: admin user create failed: {Detail}",
                tenant.Id,
                detail);
            return (null, $"Admin user creation failed: {detail}");
        }

        var roleAdd = await _userManager.AddToRoleAsync(adminUser, Roles.Manager).ConfigureAwait(false);
        if (!roleAdd.Succeeded)
        {
            var detail = string.Join("; ", roleAdd.Errors.Select(e => e.Description));
            return (null, $"Manager role assignment failed: {detail}");
        }

        await _membershipProvisioner
            .ProvisionActiveMembershipAsync(adminUser.Id, tenant.Id, isOwner: true, cancellationToken)
            .ConfigureAwait(false);

        _db.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = adminUser.Id,
            CashRegisterId = cashRegister.Id.ToString("D"),
            CreatedAt = now,
            UpdatedAt = now,
        });

        _db.BackupScheduleConfigurations.Add(BackupScheduleConfigurationEnsure.CreateDefaultRow(tenant.Id, now));

        DateTime? trialUntil = null;
        if (grantTrialLicense && !tenant.LicenseValidUntilUtc.HasValue)
        {
            trialUntil = now.AddDays(30);
            tenant.LicenseValidUntilUtc = trialUntil;
            tenant.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Provisioned tenant {TenantId} slug {Slug}: register {RegisterId}, admin {AdminUserId}, {ProductCount} demo products (importDemoMenu={ImportDemoMenu})",
            tenant.Id,
            tenant.Slug,
            cashRegister.Id,
            adminUser.Id,
            productIds.Count,
            importDemoMenu);

        return (new TenantProvisioningResult
        {
            CashRegisterId = cashRegister.Id,
            CashRegisterNumber = cashRegister.RegisterNumber,
            AdminUserId = adminUser.Id,
            AdminEmail = resolvedEmail,
            GeneratedPassword = password,
            CategoryId = category.Id,
            ProductIds = productIds,
            TrialLicenseValidUntilUtc = trialUntil,
        }, null);
    }

    private static string ResolveAdminEmail(Tenant tenant, string? adminEmail)
    {
        var trimmed = adminEmail?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            return trimmed;

        return $"admin@{tenant.Slug}.regkasse.at";
    }

    private static List<Product> CreateDemoProducts(Guid tenantId, Category category)
    {
        var categoryLabel = category.Name;
        return new List<Product>
        {
            BuildDemoProduct(tenantId, category, categoryLabel, "Demo Produkt 1", "Demo Produkt 1 - Standard 20% MwSt", 9.99m, TaxTypes.Standard, RksvProductTypes.Standard, 1),
            BuildDemoProduct(tenantId, category, categoryLabel, "Demo Produkt 2", "Demo Produkt 2 - Besonders 13% MwSt", 19.99m, TaxTypes.Special, RksvProductTypes.Special, 2),
            BuildDemoProduct(tenantId, category, categoryLabel, "Demo Produkt 3", "Demo Produkt 3 - Ermäßigt 10% MwSt", 4.99m, TaxTypes.Reduced, RksvProductTypes.Reduced, 3),
        };
    }

    private static Product BuildDemoProduct(
        Guid tenantId,
        Category category,
        string categoryLabel,
        string name,
        string description,
        decimal price,
        int taxType,
        string rksvType,
        int sequence)
    {
        var id = Guid.NewGuid();
        return new Product
        {
            Id = id,
            TenantId = tenantId,
            Name = name,
            Description = description,
            Price = price,
            TaxType = taxType,
            TaxRate = TaxTypes.GetTaxRate(taxType),
            Category = categoryLabel,
            CategoryId = category.Id,
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "Stk",
            Cost = 0m,
            Barcode = $"DEMO-{sequence:D3}-{id:N}".Length <= 50
                ? $"DEMO-{sequence:D3}-{id:N}"
                : $"DEMO-{sequence:D3}-{id:N}"[..50],
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = rksvType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    internal static string GenerateCompliantPassword()
    {
        const string lowers = "abcdefghjkmnpqrstuvwxyz";
        const string uppers = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%&*";
        const string all = lowers + uppers + digits + symbols;

        Span<char> buffer = stackalloc char[16];
        buffer[0] = lowers[RandomNumberGenerator.GetInt32(lowers.Length)];
        buffer[1] = uppers[RandomNumberGenerator.GetInt32(uppers.Length)];
        buffer[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        buffer[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var i = 4; i < buffer.Length; i++)
            buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher–Yates shuffle
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        return new string(buffer);
    }
}
