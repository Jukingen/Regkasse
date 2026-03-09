using Microsoft.AspNetCore.Identity;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Data
{
    public static class RoleSeedData
    {
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
            if (!await roleManager.RoleExistsAsync(Roles.Admin))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
                Console.WriteLine("Admin role created successfully");
            }

            if (!await roleManager.RoleExistsAsync(Roles.Cashier))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Cashier));
                Console.WriteLine("Cashier role created successfully");
            }

            // Kellner (garson) rolü – legacy; policies use Waiter
            if (!await roleManager.RoleExistsAsync("Kellner"))
            {
                await roleManager.CreateAsync(new IdentityRole("Kellner"));
                Console.WriteLine("Kellner role created successfully");
            }

            if (!await roleManager.RoleExistsAsync(Roles.Waiter))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Waiter));
                Console.WriteLine("Waiter role created successfully");
            }

            // Auditor (salt okunur denetim)
            if (!await roleManager.RoleExistsAsync("Auditor"))
            {
                await roleManager.CreateAsync(new IdentityRole("Auditor"));
                Console.WriteLine("Auditor role created successfully");
            }

            // Demo kullanıcı rolü
            if (!await roleManager.RoleExistsAsync("Demo"))
            {
                await roleManager.CreateAsync(new IdentityRole("Demo"));
                Console.WriteLine("Demo role created successfully");
            }

            if (!await roleManager.RoleExistsAsync(Roles.Manager))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.Manager));
                Console.WriteLine("Manager role created successfully");
            }

            if (!await roleManager.RoleExistsAsync("BranchManager"))
            {
                await roleManager.CreateAsync(new IdentityRole("BranchManager"));
                Console.WriteLine("BranchManager role created successfully");
            }
            if (!await roleManager.RoleExistsAsync(Roles.SuperAdmin))
            {
                await roleManager.CreateAsync(new IdentityRole(Roles.SuperAdmin));
                Console.WriteLine("SuperAdmin role created successfully");
            }

            // Single admin role is Admin only (see Roles.cs). Do not seed legacy role names.
    }
    }
}
