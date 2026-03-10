using Microsoft.AspNetCore.Identity;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Data;

/// <summary>
/// Seeds only the canonical system roles (Admin removed; use SuperAdmin). Does not create or delete legacy roles.
/// Demo is not a role; use ApplicationUser.IsDemo. Legacy rows are removed only by data migrations — never delete here.
/// Cleanup prep: see ai/ROLE_SEED_AND_MIGRATION_NOTES.md and ai/LEGACY_ROLE_MIGRATION_PLAN.md.
/// </summary>
public static class RoleSeedData
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in Roles.Canonical)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            await roleManager.CreateAsync(new IdentityRole(roleName));
            Console.WriteLine("Role created: {0}", roleName);
        }
    }
}
