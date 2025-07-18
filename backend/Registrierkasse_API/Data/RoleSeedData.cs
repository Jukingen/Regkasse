using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;

namespace Registrierkasse_API.Data
{
    public static class RoleSeedData
    {
        public static async Task SeedRolesAndPermissionsAsync(AppDbContext context)
        {
            try
            {
                // Rolleri oluştur
                await SeedRolesAsync(context);
                
                // Yetkileri oluştur
                await SeedPermissionsAsync(context);
                
                // Rol-yetki ilişkilerini oluştur
                await SeedRolePermissionsAsync(context);
                
                // Demo kullanıcıları oluştur
                await SeedDemoUsersAsync(context);
            }
            catch (Exception ex)
            {
                throw new Exception("Rol ve yetki seed data oluşturulurken hata oluştu", ex);
            }
        }

        private static async Task SeedRolesAsync(AppDbContext context)
        {
            var roles = new[]
            {
                new Role { Name = "Cashier", Description = "Kasiyer - Satış ve ödeme işlemleri", IsActive = true },
                new Role { Name = "Admin", Description = "Yönetici - Tüm sistem yönetimi", IsActive = true },
                new Role { Name = "Manager", Description = "Müdür - Raporlama ve personel yönetimi", IsActive = true },
                new Role { Name = "Demo_Cashier", Description = "Demo Kasiyer - Test amaçlı kasiyer", IsActive = true },
                new Role { Name = "Demo_Admin", Description = "Demo Admin - Test amaçlı yönetici", IsActive = true }
            };

            foreach (var role in roles)
            {
                if (!await context.Roles.AnyAsync(r => r.Name == role.Name))
                {
                    context.Roles.Add(role);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedPermissionsAsync(AppDbContext context)
        {
            var permissions = new[]
            {
                // Satış işlemleri
                new Permission { Name = "Satış Oluşturma", Description = "Yeni satış oluşturabilir", Resource = "sales", Action = "create", IsActive = true },
                new Permission { Name = "Satış Görüntüleme", Description = "Satışları görüntüleyebilir", Resource = "sales", Action = "view", IsActive = true },
                new Permission { Name = "Satış Güncelleme", Description = "Satışları güncelleyebilir", Resource = "sales", Action = "update", IsActive = true },
                new Permission { Name = "Satış Silme", Description = "Satışları silebilir", Resource = "sales", Action = "delete", IsActive = true },

                // Ürün işlemleri
                new Permission { Name = "Ürün Görüntüleme", Description = "Ürünleri görüntüleyebilir", Resource = "products", Action = "view", IsActive = true },
                new Permission { Name = "Ürün Oluşturma", Description = "Yeni ürün oluşturabilir", Resource = "products", Action = "create", IsActive = true },
                new Permission { Name = "Ürün Güncelleme", Description = "Ürünleri güncelleyebilir", Resource = "products", Action = "update", IsActive = true },
                new Permission { Name = "Ürün Silme", Description = "Ürünleri silebilir", Resource = "products", Action = "delete", IsActive = true },

                // Sepet işlemleri
                new Permission { Name = "Sepet Yönetimi", Description = "Sepet işlemlerini yönetebilir", Resource = "cart", Action = "manage", IsActive = true },
                new Permission { Name = "Sepet Görüntüleme", Description = "Sepetleri görüntüleyebilir", Resource = "cart", Action = "view", IsActive = true },

                // Ödeme işlemleri
                new Permission { Name = "Ödeme İşlemi", Description = "Ödeme işlemlerini gerçekleştirebilir", Resource = "payment", Action = "process", IsActive = true },
                new Permission { Name = "Ödeme Görüntüleme", Description = "Ödemeleri görüntüleyebilir", Resource = "payment", Action = "view", IsActive = true },
                new Permission { Name = "Ödeme İadesi", Description = "Ödeme iadesi yapabilir", Resource = "payment", Action = "refund", IsActive = true },

                // Fatura işlemleri
                new Permission { Name = "Fatura Oluşturma", Description = "Fatura oluşturabilir", Resource = "invoice", Action = "create", IsActive = true },
                new Permission { Name = "Fatura Görüntüleme", Description = "Faturaları görüntüleyebilir", Resource = "invoice", Action = "view", IsActive = true },
                new Permission { Name = "Fatura Yazdırma", Description = "Fatura yazdırabilir", Resource = "invoice", Action = "print", IsActive = true },

                // Müşteri işlemleri
                new Permission { Name = "Müşteri Görüntüleme", Description = "Müşterileri görüntüleyebilir", Resource = "customers", Action = "view", IsActive = true },
                new Permission { Name = "Müşteri Oluşturma", Description = "Yeni müşteri oluşturabilir", Resource = "customers", Action = "create", IsActive = true },
                new Permission { Name = "Müşteri Güncelleme", Description = "Müşteri bilgilerini güncelleyebilir", Resource = "customers", Action = "update", IsActive = true },
                new Permission { Name = "Müşteri Silme", Description = "Müşteri silebilir", Resource = "customers", Action = "delete", IsActive = true },

                // Kullanıcı yönetimi
                new Permission { Name = "Kullanıcı Yönetimi", Description = "Kullanıcıları yönetebilir", Resource = "users", Action = "manage", IsActive = true },
                new Permission { Name = "Kullanıcı Görüntüleme", Description = "Kullanıcıları görüntüleyebilir", Resource = "users", Action = "view", IsActive = true },
                new Permission { Name = "Kullanıcı Oluşturma", Description = "Yeni kullanıcı oluşturabilir", Resource = "users", Action = "create", IsActive = true },
                new Permission { Name = "Kullanıcı Güncelleme", Description = "Kullanıcı bilgilerini güncelleyebilir", Resource = "users", Action = "update", IsActive = true },
                new Permission { Name = "Kullanıcı Silme", Description = "Kullanıcı silebilir", Resource = "users", Action = "delete", IsActive = true },

                // Rol yönetimi
                new Permission { Name = "Rol Yönetimi", Description = "Rolleri yönetebilir", Resource = "roles", Action = "manage", IsActive = true },

                // Sistem ayarları
                new Permission { Name = "Sistem Ayarları", Description = "Sistem ayarlarını yönetebilir", Resource = "system", Action = "settings", IsActive = true },

                // Raporlar
                new Permission { Name = "Rapor Görüntüleme", Description = "Raporları görüntüleyebilir", Resource = "reports", Action = "view", IsActive = true },
                new Permission { Name = "Rapor Oluşturma", Description = "Rapor oluşturabilir", Resource = "reports", Action = "generate", IsActive = true },
                new Permission { Name = "Rapor Dışa Aktarma", Description = "Raporları dışa aktarabilir", Resource = "reports", Action = "export", IsActive = true },

                // Denetim logları
                new Permission { Name = "Denetim Görüntüleme", Description = "Denetim loglarını görüntüleyebilir", Resource = "audit", Action = "view", IsActive = true },
                new Permission { Name = "Denetim Dışa Aktarma", Description = "Denetim loglarını dışa aktarabilir", Resource = "audit", Action = "export", IsActive = true },

                // Demo kullanıcı yönetimi
                new Permission { Name = "Demo Kullanıcı Yönetimi", Description = "Demo kullanıcıları yönetebilir", Resource = "demo", Action = "manage", IsActive = true },

                // Donanım yönetimi
                new Permission { Name = "Donanım Yönetimi", Description = "Donanım ayarlarını yönetebilir", Resource = "hardware", Action = "manage", IsActive = true },

                // Stok yönetimi
                new Permission { Name = "Stok Yönetimi", Description = "Stok işlemlerini yönetebilir", Resource = "inventory", Action = "manage", IsActive = true },

                // FinanzOnline
                new Permission { Name = "FinanzOnline Yönetimi", Description = "FinanzOnline ayarlarını yönetebilir", Resource = "finanzonline", Action = "manage", IsActive = true },

                // Yedekleme
                new Permission { Name = "Yedekleme Oluşturma", Description = "Veri yedeklemesi oluşturabilir", Resource = "backup", Action = "create", IsActive = true },
                new Permission { Name = "Yedekleme Geri Yükleme", Description = "Veri yedeklemesini geri yükleyebilir", Resource = "backup", Action = "restore", IsActive = true }
            };

            foreach (var permission in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == permission.Name))
                {
                    context.Permissions.Add(permission);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedRolePermissionsAsync(AppDbContext context)
        {
            // Kasiyer yetkileri
            var cashierRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Cashier");
            if (cashierRole != null)
            {
                var cashierPermissions = new[] { "sales.create", "sales.view", "sales.update", "cart.manage", "cart.view", "payment.process", "payment.view", "invoice.create", "invoice.view", "invoice.print", "customers.view", "customers.create", "customers.update", "products.view" };

                foreach (var perm in cashierPermissions)
                {
                    var parts = perm.Split('.');
                    var resource = parts[0];
                    var action = parts[1];
                    var permission = await context.Permissions.FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action);
                    
                    if (permission != null && !await context.RolePermissions.AnyAsync(rp => rp.RoleId == cashierRole.Id && rp.PermissionId == permission.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = cashierRole.Id,
                            PermissionId = permission.Id,
                            GrantedBy = "system",
                            IsActive = true
                        });
                    }
                }
            }

            // Admin yetkileri (tüm yetkiler)
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole != null)
            {
                var allPermissions = await context.Permissions.ToListAsync();
                
                foreach (var permission in allPermissions)
                {
                    if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionId == permission.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = adminRole.Id,
                            PermissionId = permission.Id,
                            GrantedBy = "system",
                            IsActive = true
                        });
                    }
                }
            }

            // Manager yetkileri
            var managerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Manager");
            if (managerRole != null)
            {
                var managerPermissions = new[] { "sales.create", "sales.view", "sales.update", "cart.manage", "cart.view", "payment.process", "payment.view", "invoice.create", "invoice.view", "invoice.print", "customers.view", "customers.create", "customers.update", "products.view", "reports.view", "reports.generate", "audit.view", "inventory.view", "inventory.update" };

                foreach (var perm in managerPermissions)
                {
                    var parts = perm.Split('.');
                    var resource = parts[0];
                    var action = parts[1];
                    var permission = await context.Permissions.FirstOrDefaultAsync(p => p.Resource == resource && p.Action == action);
                    
                    if (permission != null && !await context.RolePermissions.AnyAsync(rp => rp.RoleId == managerRole.Id && rp.PermissionId == permission.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = managerRole.Id,
                            PermissionId = permission.Id,
                            GrantedBy = "system",
                            IsActive = true
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedDemoUsersAsync(AppDbContext context)
        {
            // Demo kullanıcıları oluştur (eğer yoksa)
            var demoUsers = new[]
            {
                new { Username = "demo.cashier1", Email = "cashier1@demo.com", FirstName = "Ahmet", LastName = "Kasiyer", EmployeeNumber = "CASH001", AccountType = "demo", IsDemo = true },
                new { Username = "demo.cashier2", Email = "cashier2@demo.com", FirstName = "Ayşe", LastName = "Kasiyer", EmployeeNumber = "CASH002", AccountType = "demo", IsDemo = true },
                new { Username = "demo.admin1", Email = "admin1@demo.com", FirstName = "Mehmet", LastName = "Admin", EmployeeNumber = "ADMIN001", AccountType = "demo", IsDemo = true },
                new { Username = "demo.admin2", Email = "admin2@demo.com", FirstName = "Fatma", LastName = "Admin", EmployeeNumber = "ADMIN002", AccountType = "demo", IsDemo = true }
            };

            foreach (var demoUser in demoUsers)
            {
                if (!await context.Users.AnyAsync(u => u.UserName == demoUser.Username))
                {
                    var user = new ApplicationUser
                    {
                        UserName = demoUser.Username,
                        Email = demoUser.Email,
                        FirstName = demoUser.FirstName,
                        LastName = demoUser.LastName,
                        EmployeeNumber = demoUser.EmployeeNumber,
                        AccountType = demoUser.AccountType,
                        IsDemo = demoUser.IsDemo,
                        IsActive = true,
                        EmailConfirmed = true
                    };
                    // Elle Id verilmemişse, otomatik GUID ata (garanti için)
                    if (string.IsNullOrEmpty(user.Id))
                        user.Id = Guid.NewGuid().ToString();

                    context.Users.Add(user);
                    await context.SaveChangesAsync();

                    // Demo kullanıcılara rol ata
                    var roleName = demoUser.Username.Contains("admin") ? "Admin" : "Cashier";
                    var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                    
                    if (role != null)
                    {
                        context.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = role.Id,
                            AssignedBy = "system",
                            IsActive = true
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
} 