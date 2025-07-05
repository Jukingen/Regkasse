using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Registrierkasse.Models;
using System.Text.Json;
using System.Collections.Generic;

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

            // Admin kullanıcısını zorla ekle (koşul kaldırıldı)
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

            var result = await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Administrator");

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
                    BankName = "Demo Bank",
                    BankAccount = "123456789",
                    IBAN = "AT123456789012345678",
                    BIC = "DEMOATWW",
                    Logo = "",
                    InvoiceFooter = "Vielen Dank für Ihren Einkauf!",
                    ReceiptFooter = "Bitte bewahren Sie diesen Beleg auf.",
                    DefaultCurrency = "EUR",
                    DefaultTaxRate = 20,
                    Industry = "Einzelhandel",
                    IsFinanceOnlineEnabled = false,
                    FinanceOnlineUsername = "",
                    FinanceOnlinePassword = "",
                    SignatureCertificate = ""
                };

                context.CompanySettings.Add(settings);
            }

            // Ürünler - Daha fazla demo ürün
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
                        StockQuantity = 150,
                        MinStockLevel = 50,
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
                        StockQuantity = 25,
                        MinStockLevel = 50,
                        Barcode = "1234567891"
                    },
                    new Product
                    {
                        Name = "Wiener Schnitzel",
                        Description = "Geleneksel Viyana usulü dana eti",
                        Price = 18.90m,
                        TaxType = TaxType.Standard,
                        Category = "Hauptgerichte",
                        Unit = "Stück",
                        StockQuantity = 80,
                        MinStockLevel = 30,
                        Barcode = "9001234567890"
                    },
                    new Product
                    {
                        Name = "Apfelstrudel",
                        Description = "Elmalı geleneksel Avusturya tatlısı",
                        Price = 6.50m,
                        TaxType = TaxType.Reduced,
                        Category = "Desserts",
                        Unit = "Stück",
                        StockQuantity = 45,
                        MinStockLevel = 20,
                        Barcode = "9001234567891"
                    },
                    new Product
                    {
                        Name = "Mozartkugel",
                        Description = "Geleneksel çikolata",
                        Price = 1.50m,
                        TaxType = TaxType.Reduced,
                        Category = "Desserts",
                        Unit = "Stück",
                        StockQuantity = 200,
                        MinStockLevel = 50,
                        Barcode = "9001234567892"
                    }
                );
            }

            // Müşteriler - Daha fazla demo müşteri
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
                        TaxNumber = "ATU12345678",
                        CompanyName = "Max GmbH"
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
                        TaxNumber = "ATU87654321",
                        CompanyName = "Maria KG"
                    },
                    new Customer
                    {
                        CustomerNumber = "CUST003",
                        FirstName = "Hans",
                        LastName = "Schmidt",
                        Email = "hans@example.com",
                        Phone = "+43111222333",
                        Address = "Bahnhofstraße 15",
                        City = "Salzburg",
                        PostalCode = "5020",
                        Country = "AT",
                        TaxNumber = "ATU11111111",
                        CompanyName = "Hans & Co"
                    }
                );
            }

            // Kasalar - Daha fazla demo kasa
            if (!context.CashRegisters.Any())
            {
                context.CashRegisters.AddRange(
                    new CashRegister
                    {
                        RegisterNumber = "REG001",
                        TseId = "TSE-DEMO-001",
                        KassenId = "KASSE-DEMO-001",
                        Location = "Hauptkassa",
                        StartingBalance = 200.00m,
                        CurrentBalance = 450.75m,
                        LastBalanceUpdate = DateTime.UtcNow,
                        Status = RegisterStatus.Open
                    },
                    new CashRegister
                    {
                        RegisterNumber = "REG002",
                        TseId = "TSE-DEMO-002",
                        KassenId = "KASSE-DEMO-002",
                        Location = "Bar",
                        StartingBalance = 100.00m,
                        CurrentBalance = 180.50m,
                        LastBalanceUpdate = DateTime.UtcNow,
                        Status = RegisterStatus.Open
                    },
                    new CashRegister
                    {
                        RegisterNumber = "REG003",
                        TseId = "TSE-DEMO-003",
                        KassenId = "KASSE-DEMO-003",
                        Location = "Terrasse",
                        StartingBalance = 50.00m,
                        CurrentBalance = 50.00m,
                        LastBalanceUpdate = DateTime.UtcNow,
                        Status = RegisterStatus.Closed
                    }
                );
                await context.SaveChangesAsync();
            }

            // Stok verileri
            if (!context.Inventories.Any())
            {
                var products = await context.Products.ToListAsync();
                foreach (var product in products)
                {
                    context.Inventories.Add(new Inventory
                    {
                        ProductId = product.Id,
                        CurrentStock = product.StockQuantity,
                        MinimumStock = product.MinStockLevel,
                        MaximumStock = product.StockQuantity * 2,
                        LastStockUpdate = DateTime.UtcNow,
                        Location = "Hauptlager",
                        Notes = "Demo stok verisi",
                        IsActive = true
                    });
                }
            }

            // Faturalar - Daha fazla demo fatura
            if (!context.Invoices.Any())
            {
                var customers = await context.Customers.ToListAsync();
                var products = await context.Products.ToListAsync();
                var cashRegisters = await context.CashRegisters.ToListAsync();
                
                if (customers.Any() && products.Any() && cashRegisters.Any())
                {
                    // İlk fatura
                    var invoice1 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-001",
                        CashRegisterId = cashRegisters[0].Id,
                        ReceiptNumber = "AT-DEMO-20240611-0001",
                        TseSignature = "DEMO_SIGNATURE_123456789",
                        IsPrinted = true,
                        TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 })),
                        TaxSummary = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            StandardTaxBase = 12.50m, 
                            StandardTaxAmount = 2.50m, 
                            TotalAmount = 15.00m 
                        })),
                        PaymentDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            PaymentMethod = "Card", 
                            Amount = 15.00m, 
                            Currency = "EUR" 
                        })),
                        InvoiceDate = DateTime.UtcNow.AddDays(-1),
                        TotalAmount = 15.00m,
                        TaxAmount = 2.50m,
                        PaymentMethod = PaymentMethod.Card,
                        PaymentStatus = "Paid",
                        Status = "Completed",
                        CustomerId = customers[0].Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard"
                    };

                    // İkinci fatura (sadece 1 kasa varsa aynı kasa kullan)
                    var invoice2 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-002",
                        CashRegisterId = cashRegisters.Count > 1 ? cashRegisters[1].Id : cashRegisters[0].Id,
                        ReceiptNumber = "AT-DEMO-20240611-0002",
                        TseSignature = "DEMO_SIGNATURE_987654321",
                        IsPrinted = true,
                        TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 })),
                        TaxSummary = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            StandardTaxBase = 21.20m, 
                            StandardTaxAmount = 4.23m, 
                            TotalAmount = 25.40m 
                        })),
                        PaymentDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            PaymentMethod = "Cash", 
                            Amount = 25.40m, 
                            Currency = "EUR" 
                        })),
                        InvoiceDate = DateTime.UtcNow.AddDays(-2),
                        TotalAmount = 25.40m,
                        TaxAmount = 4.23m,
                        PaymentMethod = PaymentMethod.Cash,
                        PaymentStatus = "Paid",
                        Status = "Completed",
                        CustomerId = customers.Count > 1 ? customers[1].Id : customers[0].Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard"
                    };

                    // Üçüncü fatura (bekleyen)
                    var invoice3 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-003",
                        CashRegisterId = cashRegisters[0].Id,
                        ReceiptNumber = "AT-DEMO-20240611-0003",
                        TseSignature = "DEMO_SIGNATURE_456789123",
                        IsPrinted = false,
                        TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { standard = 20, reduced = 10, special = 13 })),
                        TaxSummary = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            StandardTaxBase = 7.08m, 
                            StandardTaxAmount = 1.42m, 
                            TotalAmount = 8.50m 
                        })),
                        PaymentDetails = JsonDocument.Parse(JsonSerializer.Serialize(new { 
                            PaymentMethod = "Card", 
                            Amount = 8.50m, 
                            Currency = "EUR" 
                        })),
                        InvoiceDate = DateTime.UtcNow,
                        TotalAmount = 8.50m,
                        TaxAmount = 1.42m,
                        PaymentMethod = PaymentMethod.Card,
                        PaymentStatus = "Pending",
                        Status = "Pending",
                        CustomerId = customers.Count > 2 ? customers[2].Id : customers[0].Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard"
                    };

                    context.Invoices.AddRange(invoice1, invoice2, invoice3);
                    await context.SaveChangesAsync();

                    // Fatura kalemleri - güvenli array erişimi
                    var invoiceItems = new List<InvoiceItem>();
                    
                    // İlk fatura kalemleri
                    if (products.Count > 0)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice1.Id,
                            ProductId = products[0].Id, // Espresso
                            Quantity = 2,
                            UnitPrice = 2.50m,
                            TotalAmount = 5.00m,
                            TaxAmount = 1.00m
                        });
                    }
                    
                    if (products.Count > 1)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice1.Id,
                            ProductId = products[1].Id, // Cappuccino
                            Quantity = 1,
                            UnitPrice = 3.50m,
                            TotalAmount = 3.50m,
                            TaxAmount = 0.70m
                        });
                    }
                    
                    // İkinci fatura kalemleri
                    if (products.Count > 2)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice2.Id,
                            ProductId = products[2].Id, // Wiener Schnitzel
                            Quantity = 1,
                            UnitPrice = 18.90m,
                            TotalAmount = 18.90m,
                            TaxAmount = 3.78m
                        });
                    }
                    
                    if (products.Count > 3)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice2.Id,
                            ProductId = products[3].Id, // Apfelstrudel
                            Quantity = 1,
                            UnitPrice = 6.50m,
                            TotalAmount = 6.50m,
                            TaxAmount = 0.45m
                        });
                    }
                    
                    // Üçüncü fatura kalemleri
                    if (products.Count > 1)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice3.Id,
                            ProductId = products[1].Id, // Cappuccino
                            Quantity = 2,
                            UnitPrice = 3.50m,
                            TotalAmount = 7.00m,
                            TaxAmount = 1.40m
                        });
                    }
                    
                    if (products.Count > 4)
                    {
                        invoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice3.Id,
                            ProductId = products[4].Id, // Mozartkugel
                            Quantity = 1,
                            UnitPrice = 1.50m,
                            TotalAmount = 1.50m,
                            TaxAmount = 0.02m
                        });
                    }
                    
                    context.InvoiceItems.AddRange(invoiceItems);
                }
            }

            // Audit Logs - Daha fazla demo log
            if (!context.AuditLogs.Any())
            {
                context.AuditLogs.AddRange(
                    new AuditLog
                    {
                        UserId = "demo-user",
                        UserName = "Demo User",
                        Action = "Login",
                        EntityType = "User",
                        EntityId = "demo-entity",
                        OldValues = "",
                        NewValues = "",
                        Status = "Success",
                        IpAddress = "127.0.0.1",
                        UserAgent = "Demo Browser",
                        CreatedAt = DateTime.UtcNow.AddHours(-1)
                    },
                    new AuditLog
                    {
                        UserId = "demo-user",
                        UserName = "Demo User",
                        Action = "Create",
                        EntityType = "Invoice",
                        EntityId = "demo-invoice-1",
                        OldValues = "",
                        NewValues = "{\"totalAmount\": 15.00}",
                        Status = "Success",
                        IpAddress = "127.0.0.1",
                        UserAgent = "Demo Browser",
                        CreatedAt = DateTime.UtcNow.AddHours(-2)
                    },
                    new AuditLog
                    {
                        UserId = "demo-user",
                        UserName = "Demo User",
                        Action = "Update",
                        EntityType = "Product",
                        EntityId = "demo-product-1",
                        OldValues = "{\"stockQuantity\": 100}",
                        NewValues = "{\"stockQuantity\": 95}",
                        Status = "Success",
                        IpAddress = "127.0.0.1",
                        UserAgent = "Demo Browser",
                        CreatedAt = DateTime.UtcNow.AddHours(-3)
                    }
                );
            }

            // System Configuration
            if (!context.SystemConfigurations.Any())
            {
                context.SystemConfigurations.Add(new SystemConfiguration
                {
                    OperationMode = "online-only",
                    OfflineSettings = new OfflineSettings
                    {
                        Enabled = false,
                        SyncInterval = 5,
                        MaxOfflineDays = 7,
                        AutoSync = false
                    },
                    TseSettings = new TseSettings
                    {
                        Required = true,
                        OfflineAllowed = false,
                        MaxOfflineTransactions = 100
                    },
                    PrinterSettings = new PrinterSettings
                    {
                        Required = true,
                        OfflineQueue = false,
                        MaxQueueSize = 50
                    }
                });
            }

            await context.SaveChangesAsync();
        }
    }
} 