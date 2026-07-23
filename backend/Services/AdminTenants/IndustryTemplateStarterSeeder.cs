using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public interface IIndustryTemplateStarterSeeder
{
    Task<int> SeedMissingStartersAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class IndustryTemplateStarterSeeder : IIndustryTemplateStarterSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTenantMembershipProvisioner _membershipProvisioner;
    private readonly ILogger<IndustryTemplateStarterSeeder> _logger;

    public IndustryTemplateStarterSeeder(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserTenantMembershipProvisioner membershipProvisioner,
        ILogger<IndustryTemplateStarterSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _membershipProvisioner = membershipProvisioner;
        _logger = logger;
    }

    public async Task<int> SeedMissingStartersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return 0;

        var template = IndustryPermissionTemplates.Get(tenant.IndustryTemplateId);
        if (template is null)
            return 0;

        var cashRegisterId = await _db.CashRegisters
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderByDescending(c => c.IsDefaultForTenant)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var created = 0;

        foreach (var slot in template.Slots.Where(s => s.SeedStarterUser))
        {
            var email = $"{slot.Key}@{tenant.Slug}.regkasse.at";
            if (await _userManager.FindByEmailAsync(email) != null)
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

            var create = await _userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                _logger.LogWarning(
                    "Industry starter create failed for {Email}: {Errors}",
                    email,
                    string.Join("; ", create.Errors.Select(e => e.Description)));
                continue;
            }

            await _userManager.AddToRoleAsync(user, slot.SystemRole);
            await _membershipProvisioner.ProvisionActiveMembershipAsync(
                user.Id, tenant.Id, isOwner: false, cancellationToken);
            created++;
        }

        return created;
    }
}
