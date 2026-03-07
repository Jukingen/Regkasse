using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Data
{
    public static class RoleSeedData
    {
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
            // Administrator: FE-Admin ve API [Authorize(Roles = "Administrator")] ile uyumlu
            if (!await roleManager.RoleExistsAsync("Administrator"))
            {
                await roleManager.CreateAsync(new IdentityRole("Administrator"));
                Console.WriteLine("Administrator role created successfully");
            }

            // Admin rolü (geriye uyumluluk)
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
                Console.WriteLine("Admin role created successfully");
            }

            // Kasiyer rolü
            if (!await roleManager.RoleExistsAsync("Cashier"))
            {
                await roleManager.CreateAsync(new IdentityRole("Cashier"));
                Console.WriteLine("Cashier role created successfully");
            }

            // Kellner (garson) rolü
            if (!await roleManager.RoleExistsAsync("Kellner"))
            {
                await roleManager.CreateAsync(new IdentityRole("Kellner"));
                Console.WriteLine("Kellner role created successfully");
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

            // Manager rolü
            if (!await roleManager.RoleExistsAsync("Manager"))
            {
                await roleManager.CreateAsync(new IdentityRole("Manager"));
                Console.WriteLine("Manager role created successfully");
            }
    }
    }
}
