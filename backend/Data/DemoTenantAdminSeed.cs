using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Data;

/// <summary>
/// Idempotent seed for dev/prod demo tenants and Manager admin users (admin@{slug}.regkasse.at).
/// Complements migration <c>SeedDemoTenantAdmins</c> and <c>ReplaceDemoCafeBarWithProd</c>.
/// </summary>
public static class DemoTenantAdminSeed
{
    /// <summary>Documented default password for all demo tenant admins (local dev only).</summary>
    public const string DefaultPassword = "DemoTenant1!";

    private sealed record DemoTenantAdminSpec(
        Guid TenantId,
        string Slug,
        string DisplayName,
        string UserId,
        string Email,
        string EmployeeNumber,
        string TaxNumber);

    private static readonly DemoTenantAdminSpec[] Specs =
    {
        new(
            DemoTenantIds.Dev,
            "dev",
            "Development",
            DemoTenantAdminUserIds.Dev,
            "admin@dev.regkasse.at",
            "DEMO-DEV-001",
            "ATU10000001"),
        new(
            DemoTenantIds.Prod,
            "prod",
            "Production",
            DemoTenantAdminUserIds.Prod,
            "admin@prod.regkasse.at",
            "DEMO-PROD-001",
            "ATU10000002"),
    };

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

        foreach (var spec in Specs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == spec.Slug, cancellationToken)
                .ConfigureAwait(false);

            if (!ShouldProvisionDemoTenant(tenant, spec.Slug))
            {
                if (tenant != null)
                    Console.WriteLine("Demo tenant seed skipped (inactive or deleted): {0}", spec.Slug);
                continue;
            }

            if (tenant != null
                && IsDevelopmentPresetSlug(spec.Slug)
                && TryReactivateDevelopmentPresetTenant(tenant, now))
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Demo tenant reactivated in Development: {0}", spec.Slug);
            }

            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = spec.TenantId,
                    Name = spec.DisplayName,
                    Slug = spec.Slug,
                    Email = spec.Email,
                    Status = TenantStatuses.Active,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Demo tenant created: {0} ({1})", spec.DisplayName, spec.Slug);
            }

            var user = await userManager.FindByEmailAsync(spec.Email).ConfigureAwait(false);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Id = spec.UserId,
                    UserName = spec.Email,
                    Email = spec.Email,
                    FirstName = "Admin",
                    LastName = spec.DisplayName,
                    EmployeeNumber = spec.EmployeeNumber,
                    Role = Roles.Manager,
                    TaxNumber = spec.TaxNumber,
                    Notes = $"Demo tenant admin for {spec.Slug} (seeded)",
                    IsActive = true,
                    EmailConfirmed = true,
                    AccountType = "Admin",
                    IsDemo = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                };

                var create = await userManager.CreateAsync(user, DefaultPassword).ConfigureAwait(false);
                if (!create.Succeeded)
                {
                    Console.WriteLine(
                        "Demo tenant admin create failed for {0}: {1}",
                        spec.Email,
                        string.Join("; ", create.Errors.Select(e => e.Description)));
                    continue;
                }

                var roleAdd = await userManager.AddToRoleAsync(user, Roles.Manager).ConfigureAwait(false);
                if (!roleAdd.Succeeded)
                {
                    Console.WriteLine(
                        "Demo tenant admin role failed for {0}: {1}",
                        spec.Email,
                        string.Join("; ", create.Errors.Select(e => e.Description)));
                    continue;
                }

                Console.WriteLine("Demo tenant admin created: {0} (password: documented in DemoTenantAdminSeed)", spec.Email);
            }
            else
            {
                var changed = false;
                if (!string.Equals(user.Role, Roles.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = Roles.Manager;
                    changed = true;
                }

                if (!user.IsActive)
                {
                    user.IsActive = true;
                    changed = true;
                }

                if (changed)
                {
                    user.UpdatedAt = now;
                    await userManager.UpdateAsync(user).ConfigureAwait(false);
                }

                var roles = await userManager.GetRolesAsync(user).ConfigureAwait(false);
                if (!roles.Contains(Roles.Manager, StringComparer.OrdinalIgnoreCase))
                {
                    if (roles.Count > 0)
                        await userManager.RemoveFromRolesAsync(user, roles).ConfigureAwait(false);
                    await userManager.AddToRoleAsync(user, Roles.Manager).ConfigureAwait(false);
                }
            }

            await membershipProvisioner
                .ProvisionActiveMembershipAsync(user.Id, tenant.Id, isOwner: true, cancellationToken)
                .ConfigureAwait(false);
        }

        await ArchiveLeftoverDemoTenantsAsync(db, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds operational demo tenants. Missing <c>dev</c> is created; missing/deleted <c>prod</c> is not.
    /// Suspended <c>dev</c> is reactivated in Development (see <see cref="TryReactivateDevelopmentPresetTenant"/>).
    /// </summary>
    private static bool ShouldProvisionDemoTenant(Tenant? tenant, string slug)
    {
        if (tenant != null)
        {
            if (TenantStatuses.IsRemoved(tenant.Status))
                return false;

            if (IsDevelopmentPresetSlug(slug))
                return true;

            return tenant.IsActive
                   && string.Equals(tenant.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(slug, "dev", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Soft-delete leftover Test Cafe / Test Bar rows so they leave switcher and cash-register views.
    /// Platform sentinel is never archived here (FK continuity).
    /// </summary>
    internal static async Task ArchiveLeftoverDemoTenantsAsync(
        AppDbContext db,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var leftover = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => LeftoverDemoTenantSlugs.StorageSlugs.Contains(t.Slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        leftover.RemoveAll(t =>
            SystemTenantIds.IsPlatformTenantId(t.Id) || SystemTenantIds.IsPlatformSlug(t.Slug));

        if (leftover.Count == 0)
            return;

        var changed = false;
        foreach (var tenant in leftover)
        {
            if (TenantStatuses.IsRemoved(tenant.Status) && !tenant.IsActive)
                continue;

            tenant.Status = TenantStatuses.Cancelled;
            tenant.IsActive = false;
            tenant.DeletedAtUtc ??= utcNow;
            tenant.UpdatedAt = utcNow;
            changed = true;
            Console.WriteLine("Leftover demo tenant soft-deleted: {0} ({1})", tenant.Name, tenant.Slug);
        }

        if (!changed)
            return;

        var leftoverIds = leftover.Select(t => t.Id).ToList();
        var memberships = await db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => leftoverIds.Contains(m.TenantId) && m.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var membership in memberships)
            membership.IsActive = false;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsDevelopmentPresetSlug(string slug) =>
        string.Equals(slug, "dev", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Local DX: keep seeded <c>dev</c> bindable. Does not resurrect archived/cancelled rows.
    /// </summary>
    internal static bool TryReactivateDevelopmentPresetTenant(Tenant tenant, DateTime utcNow)
    {
        if (!IsDevelopmentPresetSlug(tenant.Slug) || TenantStatuses.IsRemoved(tenant.Status))
            return false;

        var changed = false;
        if (!tenant.IsActive)
        {
            tenant.IsActive = true;
            changed = true;
        }

        if (!string.Equals(tenant.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            tenant.Status = TenantStatuses.Active;
            changed = true;
        }

        if (changed)
            tenant.UpdatedAt = utcNow;

        return changed;
    }
}
