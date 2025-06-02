using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Registrierkasse.Models;

namespace Registrierkasse.Data
{
    public static class SeedData
    {
        public enum UserRole
        {
            Admin,
            Manager,
            Cashier,
            Waiter
        }

        public static async Task Initialize(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Rolleri oluştur
            string[] roles = { "Administrator", "Manager", "Cashier", "Accountant" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin kullanıcısı oluştur
            if (!context.Users.Any())
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@admin.com",
                    Email = "admin@admin.com",
                    FirstName = "Admin",
                    LastName = "User",
                    EmployeeNumber = "EMP001",
                    EmailConfirmed = true,
                    Role = "Admin"
                };

                var result = await userManager.CreateAsync(admin, "Abcd#1234");
                await userManager.AddToRoleAsync(admin, "Administrator");
            }

            // Şirket ayarlarını oluştur
            if (!context.CompanySettings.Any())
            {
                var settings = new CompanySettings
                {
                    CompanyName = "Demo GmbH",
                    TaxNumber = "ATU12345678",
                    VATNumber = "ATU12345678",
                    Address = "Hauptstraße 1",
                    City = "Wien",
                    PostalCode = "1010",
                    Country = "Österreich",
                    Phone = "+43 1 234567",
                    Email = "office@demo.at",
                    Website = "www.demo.at",
                    DefaultCurrency = "EUR",
                    DefaultTaxRate = 20,
                    Industry = "Einzelhandel"
                };

                context.CompanySettings.Add(settings);
            }

            // Ürünler
            if (!context.Products.Any())
            {
                context.Products.AddRange(
                    new Product
                    {
                        Name = "Espresso",
                        Description = "Tek shot espresso",
                        Price = 2.50m,
                        TaxType = TaxType.Standard,
                        Category = "Kahve",
                        Unit = "Adet",
                        StockQuantity = 1000,
                        MinStockLevel = 100,
                        Barcode = "1234567890"
                    },
                    new Product
                    {
                        Name = "Cappuccino",
                        Description = "Espresso ve buharla ısıtılmış süt",
                        Price = 3.50m,
                        TaxType = TaxType.Standard,
                        Category = "Kahve",
                        Unit = "Adet",
                        StockQuantity = 1000,
                        MinStockLevel = 100,
                        Barcode = "1234567891"
                    }
                );
            }

            // Müşteriler
            if (!context.Customers.Any())
            {
                context.Customers.AddRange(
                    new Customer
                    {
                        CustomerNumber = "CUST001",
                        FirstName = "Max",
                        LastName = "Mustermann",
                        Email = "max@example.com",
                        Phone = "+43123456789",
                        Address = "Hauptstraße 1",
                        City = "Wien",
                        PostalCode = "1010",
                        Country = "AT",
                        TaxNumber = "ATU12345678"
                    },
                    new Customer
                    {
                        CustomerNumber = "CUST002",
                        FirstName = "Maria",
                        LastName = "Musterfrau",
                        Email = "maria@example.com",
                        Phone = "+43987654321",
                        Address = "Nebenstraße 2",
                        City = "Graz",
                        PostalCode = "8010",
                        Country = "AT",
                        TaxNumber = "ATU87654321"
                    }
                );
            }

            // Örnek kasa oluştur
            if (!context.CashRegisters.Any())
            {
                var register = new CashRegister
                {
                    RegisterNumber = "REG001",
                    Location = "Hauptkassa",
                    StartingBalance = 200.00m,
                    CurrentBalance = 200.00m,
                    LastBalanceUpdate = DateTime.UtcNow,
                    Status = RegisterStatus.Closed
                };

                context.CashRegisters.Add(register);
            }

            await context.SaveChangesAsync();
        }
    }
} 