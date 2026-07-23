using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
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
    private readonly IPaymentMethodDefinitionBootstrapService _paymentMethodBootstrap;
    private readonly ITseProvisioningService _tseProvisioning;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        IUserUniquenessValidationService uniquenessValidation,
        IDemoProductImportService demoProductImport,
        IPaymentMethodDefinitionBootstrapService paymentMethodBootstrap,
        ITseProvisioningService tseProvisioning,
        ILogger<TenantProvisioningService> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _uniquenessValidation = uniquenessValidation;
        _demoProductImport = demoProductImport;
        _paymentMethodBootstrap = paymentMethodBootstrap;
        _tseProvisioning = tseProvisioning;
        _logger = logger;
    }

    public async Task<(TenantProvisioningResult? Result, string? Error)> ProvisionAsync(
        Tenant tenant,
        string? adminEmail,
        string? adminPassword,
        bool grantTrialLicense,
        bool importDemoMenu = false,
        string? cashRegisterNumber = null,
        bool seedIndustryStarterUsers = true,
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
            ? PasswordGenerator.GenerateSecurePassword(16)
            : adminPassword.Trim();

        if (password.Length < 8)
            return (null, "Admin password must be at least 8 characters.");

        var resolvedRegisterNumber = ResolveRegisterNumber(cashRegisterNumber);

        var now = DateTime.UtcNow;
        var cashRegister = new CashRegister
        {
            TenantId = tenant.Id,
            RegisterNumber = resolvedRegisterNumber,
            Location = DefaultRegisterLocation,
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = now,
            Status = RegisterStatus.Closed,
            CreatedAt = now,
            IsActive = true,
            IsDefaultForTenant = true,
        };
        _db.CashRegisters.Add(cashRegister);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var tseResult = await _tseProvisioning
            .ProvisionTseForCashRegisterAsync(cashRegister.Id, force: false, cancellationToken)
            .ConfigureAwait(false);
        if (!tseResult.IsSuccess)
        {
            return (null, tseResult.Error ?? "TSE provisioning failed.");
        }

        Category category;
        IReadOnlyList<Guid> productIds;

        if (importDemoMenu)
        {
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

        await _paymentMethodBootstrap
            .EnsureDefaultsForCashRegisterAsync(tenant.Id, cashRegister.Id, cancellationToken)
            .ConfigureAwait(false);

        if (seedIndustryStarterUsers)
        {
            await SeedIndustryStarterUsersAsync(tenant, cashRegister.Id, now, cancellationToken)
                .ConfigureAwait(false);
        }

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
            TseDeviceId = tseResult.Device?.Id,
            TseProvisioned = tseResult.Outcome == TseProvisioningOutcome.Success,
        }, null);
    }

    private async Task SeedIndustryStarterUsersAsync(
        Tenant tenant,
        Guid cashRegisterId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var template = IndustryPermissionTemplates.Get(tenant.IndustryTemplateId);
        if (template is null)
            return;

        foreach (var slot in template.Slots.Where(s => s.SeedStarterUser))
        {
            var email = $"{slot.Key}@{tenant.Slug}.regkasse.at";
            if (await _userManager.FindByEmailAsync(email).ConfigureAwait(false) != null)
                continue;

            var password = PasswordGenerator.GenerateSecurePassword(16);
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = slot.DisplayNameDe,
                LastName = tenant.Name.Length > 40 ? tenant.Name[..40] : tenant.Name,
                EmployeeNumber = $"SEED-{slot.Key}".ToUpperInvariant(),
                Role = slot.SystemRole,
                Notes = $"Industry template starter ({template.Id}/{slot.Key})",
                IsActive = false,
                EmailConfirmed = true,
                AccountType = "Staff",
                IsDemo = false,
                CreatedAt = now,
                UpdatedAt = now,
                CashRegisterId = cashRegisterId,
                MustChangePasswordOnNextLogin = true,
            };

            var create = await _userManager.CreateAsync(user, password).ConfigureAwait(false);
            if (!create.Succeeded)
            {
                _logger.LogWarning(
                    "Industry starter user create failed for {Email}: {Errors}",
                    email,
                    string.Join("; ", create.Errors.Select(e => e.Description)));
                continue;
            }

            await _userManager.AddToRoleAsync(user, slot.SystemRole).ConfigureAwait(false);
            await _membershipProvisioner
                .ProvisionActiveMembershipAsync(user.Id, tenant.Id, isOwner: false, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string ResolveAdminEmail(Tenant tenant, string? adminEmail)
    {
        var trimmed = adminEmail?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
            return trimmed;

        return $"admin@{tenant.Slug}.regkasse.at";
    }

    private static string ResolveRegisterNumber(string? cashRegisterNumber)
    {
        var trimmed = cashRegisterNumber?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return DefaultRegisterNumber;

        return trimmed.Length > 20 ? trimmed[..20] : trimmed;
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

}
