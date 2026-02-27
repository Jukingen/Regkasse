using Microsoft.AspNetCore.Identity;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Data
{
    public static class UserSeedData
    {
        public static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
        {
            // Eski kullanıcıları sil
            var oldAdmin = await userManager.FindByEmailAsync("admin@kasse.com");
            if (oldAdmin != null)
            {
                await userManager.DeleteAsync(oldAdmin);
                Console.WriteLine("Old admin user deleted");
            }

            var oldDemo = await userManager.FindByEmailAsync("demo@kasse.com");
            if (oldDemo != null)
            {
                await userManager.DeleteAsync(oldDemo);
                Console.WriteLine("Old demo user deleted");
            }

            // Admin kullanıcısı var mı kontrol et
            if (await userManager.FindByEmailAsync("admin@admin.com") == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = "admin@admin.com",
                    Email = "admin@admin.com",
                    FirstName = "Admin",
                    LastName = "User",
                    EmployeeNumber = "EMP001",
                    Role = "Admin",
                    TaxNumber = "ATU12345678",
                    Notes = "Initial admin user",
                    IsActive = true,
                    AccountType = "Admin",
                    IsDemo = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    Console.WriteLine("Admin user created successfully");
                }
                else
                {
                    Console.WriteLine("Failed to create admin user:");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"- {error.Description}");
                    }
                }
            }

            // Demo kasiyer kullanıcısı var mı kontrol et
            if (await userManager.FindByEmailAsync("demo@demo.com") == null)
            {
                var demoUser = new ApplicationUser
                {
                    UserName = "demo@demo.com",
                    Email = "demo@demo.com",
                    FirstName = "Demo",
                    LastName = "Cashier",
                    EmployeeNumber = "EMP002",
                    Role = "Cashier",
                    TaxNumber = "ATU87654321",
                    Notes = "Demo cashier user",
                    IsActive = true,
                    AccountType = "Cashier",
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(demoUser, "Demo123!");
                
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(demoUser, "Cashier");
                    Console.WriteLine("Demo cashier user created successfully");
                }
                else
                {
                    Console.WriteLine("Failed to create demo user:");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"- {error.Description}");
                    }
                }
            }
        }
    }
}
