using Microsoft.AspNetCore.Identity;

namespace KasseAPI_Final.Data
{
    public static class RoleSeedData
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            // Admin rolü
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
