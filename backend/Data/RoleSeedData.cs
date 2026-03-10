using Microsoft.AspNetCore.Identity;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Data;

/// <summary>
/// Seeds only the 8 canonical system roles. Does not create or delete legacy roles (Administrator, Kellner, BranchManager, Auditor, Demo).
/// Legacy roles may still exist in DB from previous seeds; handle them via data migration or admin reassignment.
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
