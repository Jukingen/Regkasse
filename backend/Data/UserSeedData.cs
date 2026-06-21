using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Data
{
    public static class UserSeedData
    {
        public static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, IUserTenantMembershipProvisioner tenantMembershipProvisioner)
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

            // Admin kullanıcısı: yoksa oluştur (SuperAdmin), varsa rolünü SuperAdmin yap
            var adminUser = await userManager.FindByEmailAsync("admin@admin.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@admin.com",
                    Email = "admin@admin.com",
                    FirstName = "Admin",
                    LastName = "User",
                    EmployeeNumber = "SUPER-ADMIN-001",
                    Role = Roles.SuperAdmin,
                    TaxNumber = "ATU99999901",
                    Notes = "Initial admin user",
                    IsActive = true,
                    AccountType = "Admin",
                    IsDemo = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                try
                {
                    var result = await userManager.CreateAsync(adminUser, "Admin123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);
                        await tenantMembershipProvisioner.ProvisionActiveMembershipAsync(adminUser.Id, LegacyDefaultTenantIds.Primary);
                        Console.WriteLine("Admin user created successfully (role: SuperAdmin)");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create admin user:");
                        foreach (var error in result.Errors)
                            Console.WriteLine($"- {error.Description}");
                    }
                }
                catch (DbUpdateException ex)
                {
                    Console.WriteLine($"Admin user seed skipped (database conflict): {ex.InnerException?.Message ?? ex.Message}");
                    adminUser = await userManager.FindByEmailAsync("admin@admin.com");
                }
            }
            else
            {
                // Mevcut admin@admin.com kullanıcısını SuperAdmin yap
                if (adminUser.Role != Roles.SuperAdmin)
                {
                    adminUser.Role = Roles.SuperAdmin;
                    adminUser.UpdatedAt = DateTime.UtcNow;
                    await userManager.UpdateAsync(adminUser);
                    var currentRoles = await userManager.GetRolesAsync(adminUser);
                    if (currentRoles.Count > 0)
                        await userManager.RemoveFromRolesAsync(adminUser, currentRoles);
                    await userManager.AddToRoleAsync(adminUser, Roles.SuperAdmin);
                    Console.WriteLine("admin@admin.com updated to role SuperAdmin");
                }

                await tenantMembershipProvisioner.ProvisionActiveMembershipAsync(adminUser.Id, LegacyDefaultTenantIds.Primary);
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
                    EmployeeNumber = "DEMO-CASHIER-001",
                    Role = Roles.Cashier,
                    TaxNumber = "ATU99999902",
                    Notes = "Demo cashier user",
                    IsActive = true,
                    AccountType = "Cashier",
                    IsDemo = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                try
                {
                    var result = await userManager.CreateAsync(demoUser, "Demo123!");

                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(demoUser, Roles.Cashier);
                        await tenantMembershipProvisioner.ProvisionActiveMembershipAsync(demoUser.Id, LegacyDefaultTenantIds.Primary);
                        Console.WriteLine("Demo cashier user created successfully");
                    }
                    else
                    {
                        Console.WriteLine("Failed to create demo user:");
                        foreach (var error in result.Errors)
                            Console.WriteLine($"- {error.Description}");
                    }
                }
                catch (DbUpdateException ex)
                {
                    Console.WriteLine($"Demo user seed skipped (database conflict): {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            await EnsureSeedUserMembershipAsync(tenantMembershipProvisioner, userManager, "demo@demo.com");
        }

        private static async Task EnsureSeedUserMembershipAsync(
            IUserTenantMembershipProvisioner provisioner,
            UserManager<ApplicationUser> userManager,
            string email)
        {
            var u = await userManager.FindByEmailAsync(email);
            if (u != null)
                await provisioner.ProvisionActiveMembershipAsync(u.Id, LegacyDefaultTenantIds.Primary);
        }
    }
}
