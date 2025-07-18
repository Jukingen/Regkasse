using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Registrierkasse_API.Models;
using System.Text.Json;
using System.Collections.Generic;

namespace Registrierkasse_API.Data
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

            // Admin kullanıcısını oluştur veya mevcut olanı al
            var adminEmail = "admin@admin.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    // Türkçe açıklama: Eğer elle Id verilmezse, Identity otomatik olarak GUID atar.
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "User",
                    EmployeeNumber = "EMP001",
                    EmailConfirmed = true,
                    Role = "Admin"
                };
                // Elle Id verilmemişse, otomatik GUID ata (garanti için)
                if (string.IsNullOrEmpty(admin.Id))
                    admin.Id = Guid.NewGuid().ToString();

                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (!result.Succeeded)
                {
                    throw new Exception($"Admin kullanıcısı oluşturulamadı: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // Admin rolünü ata (eğer yoksa)
            if (!await userManager.IsInRoleAsync(admin, "Administrator"))
            {
                var roleResult = await userManager.AddToRoleAsync(admin, "Administrator");
                if (!roleResult.Succeeded)
                {
                    throw new Exception($"Admin rolü atanamadı: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                }
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
                    // Standard Tax (20%) - Getränke
                    new Product
                    {
                        Name = "Espresso",
                        Description = "Ein starker italienischer Kaffee",
                        Price = 2.50m,
                        TaxType = TaxType.Standard,
                        Category = "Getränke",
                        Unit = "Stück",
                        StockQuantity = 150,
                        MinStockLevel = 50,
                        Barcode = "4001234567890",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Cappuccino",
                        Description = "Espresso mit aufgeschäumter Milch",
                        Price = 3.80m,
                        TaxType = TaxType.Standard,
                        Category = "Getränke",
                        Unit = "Stück",
                        StockQuantity = 120,
                        MinStockLevel = 40,
                        Barcode = "4001234567891",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Latte Macchiato",
                        Description = "Milch mit Espresso",
                        Price = 4.20m,
                        TaxType = TaxType.Standard,
                        Category = "Getränke",
                        Unit = "Stück",
                        StockQuantity = 100,
                        MinStockLevel = 30,
                        Barcode = "4001234567892",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Mineralwasser",
                        Description = "Natürliches Mineralwasser 0,5L",
                        Price = 2.00m,
                        TaxType = TaxType.Standard,
                        Category = "Getränke",
                        Unit = "Flasche",
                        StockQuantity = 200,
                        MinStockLevel = 60,
                        Barcode = "4001234567893",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Cola",
                        Description = "Erfrischendes Cola-Getränk 0,33L",
                        Price = 2.80m,
                        TaxType = TaxType.Standard,
                        Category = "Getränke",
                        Unit = "Dose",
                        StockQuantity = 180,
                        MinStockLevel = 50,
                        Barcode = "4001234567894",
                        TaxRate = 20.0m
                    },
                    
                    // Standard Tax (20%) - Hauptgerichte
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
                        Barcode = "4001234567895",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Gulasch",
                        Description = "Ungarisches Rindfleischgulasch mit Kartoffeln",
                        Price = 14.50m,
                        TaxType = TaxType.Standard,
                        Category = "Hauptgerichte",
                        Unit = "Portion",
                        StockQuantity = 60,
                        MinStockLevel = 20,
                        Barcode = "4001234567896",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Kaiserschmarrn",
                        Description = "Geleneksel Avusturya tatlısı",
                        Price = 12.80m,
                        TaxType = TaxType.Standard,
                        Category = "Hauptgerichte",
                        Unit = "Portion",
                        StockQuantity = 45,
                        MinStockLevel = 15,
                        Barcode = "4001234567897",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Tafelspitz",
                        Description = "Gekochtes Rindfleisch mit Gemüse",
                        Price = 16.90m,
                        TaxType = TaxType.Standard,
                        Category = "Hauptgerichte",
                        Unit = "Portion",
                        StockQuantity = 40,
                        MinStockLevel = 15,
                        Barcode = "4001234567898",
                        TaxRate = 20.0m
                    },
                    new Product
                    {
                        Name = "Schweinsbraten",
                        Description = "Gebratenes Schweinefleisch mit Knödel",
                        Price = 15.50m,
                        TaxType = TaxType.Standard,
                        Category = "Hauptgerichte",
                        Unit = "Portion",
                        StockQuantity = 55,
                        MinStockLevel = 20,
                        Barcode = "4001234567899",
                        TaxRate = 20.0m
                    },
                    
                    // Reduced Tax (10%) - Desserts
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
                        Barcode = "4001234567900",
                        TaxRate = 10.0m
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
                        Barcode = "4001234567901",
                        TaxRate = 10.0m
                    },
                    new Product
                    {
                        Name = "Sachertorte",
                        Description = "Klassische Wiener Schokoladentorte",
                        Price = 7.80m,
                        TaxType = TaxType.Reduced,
                        Category = "Desserts",
                        Unit = "Stück",
                        StockQuantity = 35,
                        MinStockLevel = 15,
                        Barcode = "4001234567902",
                        TaxRate = 10.0m
                    },
                    new Product
                    {
                        Name = "Kaiserschmarrn",
                        Description = "Süßer Pfannkuchen mit Rosinen",
                        Price = 8.90m,
                        TaxType = TaxType.Reduced,
                        Category = "Desserts",
                        Unit = "Portion",
                        StockQuantity = 30,
                        MinStockLevel = 10,
                        Barcode = "4001234567903",
                        TaxRate = 10.0m
                    },
                    new Product
                    {
                        Name = "Topfenstrudel",
                        Description = "Quarkstrudel mit Vanillesauce",
                        Price = 6.20m,
                        TaxType = TaxType.Reduced,
                        Category = "Desserts",
                        Unit = "Stück",
                        StockQuantity = 40,
                        MinStockLevel = 15,
                        Barcode = "4001234567904",
                        TaxRate = 10.0m
                    },
                    
                    // Special Tax (13%) - Snacks
                    new Product
                    {
                        Name = "Brezel",
                        Description = "Frische Laugenbrezel",
                        Price = 2.20m,
                        TaxType = TaxType.Special,
                        Category = "Snacks",
                        Unit = "Stück",
                        StockQuantity = 100,
                        MinStockLevel = 30,
                        Barcode = "4001234567905",
                        TaxRate = 13.0m
                    },
                    new Product
                    {
                        Name = "Käsekrainer",
                        Description = "Würstel mit Käsefüllung",
                        Price = 4.50m,
                        TaxType = TaxType.Special,
                        Category = "Snacks",
                        Unit = "Stück",
                        StockQuantity = 80,
                        MinStockLevel = 25,
                        Barcode = "4001234567906",
                        TaxRate = 13.0m
                    },
                    new Product
                    {
                        Name = "Leberkäse",
                        Description = "Bayerischer Leberkäse mit Senf",
                        Price = 3.80m,
                        TaxType = TaxType.Special,
                        Category = "Snacks",
                        Unit = "Stück",
                        StockQuantity = 70,
                        MinStockLevel = 20,
                        Barcode = "4001234567907",
                        TaxRate = 13.0m
                    },
                    new Product
                    {
                        Name = "Kartoffelsalat",
                        Description = "Hausgemachter Kartoffelsalat",
                        Price = 4.20m,
                        TaxType = TaxType.Special,
                        Category = "Snacks",
                        Unit = "Portion",
                        StockQuantity = 60,
                        MinStockLevel = 20,
                        Barcode = "4001234567908",
                        TaxRate = 13.0m
                    },
                    new Product
                    {
                        Name = "Gurkensalat",
                        Description = "Frischer Gurkensalat mit Dill",
                        Price = 3.50m,
                        TaxType = TaxType.Special,
                        Category = "Snacks",
                        Unit = "Portion",
                        StockQuantity = 50,
                        MinStockLevel = 15,
                        Barcode = "4001234567909",
                        TaxRate = 13.0m
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
                        Name = "Max Mustermann",
                        Email = "max@example.com",
                        Phone = "+43123456789",
                        Address = "Hauptstraße 1, 1010 Wien, Österreich",
                        TaxNumber = "ATU12345678",
                        Category = CustomerCategory.Regular,
                        DiscountPercentage = 0,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Notes = "Demo müşteri - Max GmbH"
                    },
                    new Customer
                    {
                        CustomerNumber = "CUST002",
                        Name = "Maria Musterfrau",
                        Email = "maria@example.com",
                        Phone = "+43987654321",
                        Address = "Nebenstraße 2, 8010 Graz, Österreich",
                        TaxNumber = "ATU87654321",
                        Category = CustomerCategory.Premium,
                        DiscountPercentage = 5,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Notes = "Demo müşteri - Maria KG"
                    },
                    new Customer
                    {
                        CustomerNumber = "CUST003",
                        Name = "Hans Schmidt",
                        Email = "hans@example.com",
                        Phone = "+43111222333",
                        Address = "Bahnhofstraße 15, 5020 Salzburg, Österreich",
                        TaxNumber = "ATU11111111",
                        Category = CustomerCategory.VIP,
                        DiscountPercentage = 10,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Notes = "Demo müşteri - Hans & Co"
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
                        ProductId = product.Id, // string yerine Guid olarak atandı
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
                    // İlk fatura (ödendi)
                    var invoice1 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-001",
                        CashRegisterId = cashRegisters[0].Id.ToString(),
                        ReceiptNumber = "AT-DEMO-20240611-0001",
                        TseSignature = "DEMO_SIGNATURE_123456789",
                        IsPrinted = true,
                        TaxDetails = JsonDocument.Parse("{\"standard\": 20, \"reduced\": 10, \"special\": 13}"),
                        InvoiceItems = JsonDocument.Parse("[]"),
                        InvoiceDate = DateTime.UtcNow.AddDays(-1),
                        TotalAmount = 15.00m,
                        TaxAmount = 2.50m,
                        PaymentMethod = PaymentMethod.Card,
                        PaymentStatus = PaymentStatus.Paid,
                        Status = InvoiceStatus.Paid,
                        CustomerId = admin.Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard",
                        CreatedById = admin.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    };

                    // İkinci fatura (sadece 1 kasa varsa aynı kasa kullan)
                    var invoice2 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-002",
                        CashRegisterId = cashRegisters.Count > 1 ? cashRegisters[1].Id.ToString() : cashRegisters[0].Id.ToString(),
                        ReceiptNumber = "AT-DEMO-20240611-0002",
                        TseSignature = "DEMO_SIGNATURE_987654321",
                        IsPrinted = true,
                        TaxDetails = JsonDocument.Parse("{\"standard\": 20, \"reduced\": 10, \"special\": 13}"),
                        InvoiceItems = JsonDocument.Parse("[]"),
                        InvoiceDate = DateTime.UtcNow.AddDays(-2),
                        TotalAmount = 25.40m,
                        TaxAmount = 4.23m,
                        PaymentMethod = PaymentMethod.Cash,
                        PaymentStatus = PaymentStatus.Paid,
                        Status = InvoiceStatus.Paid,
                        CustomerId = admin.Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard",
                        CreatedById = admin.Id,
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    };

                    // Üçüncü fatura (bekleyen)
                    var invoice3 = new Invoice
                    {
                        InvoiceNumber = "INV-2024-003",
                        CashRegisterId = cashRegisters[0].Id.ToString(),
                        ReceiptNumber = "AT-DEMO-20240611-0003",
                        TseSignature = "DEMO_SIGNATURE_456789123",
                        IsPrinted = false,
                        TaxDetails = JsonDocument.Parse("{\"standard\": 20, \"reduced\": 10, \"special\": 13}"),
                        InvoiceItems = JsonDocument.Parse("[]"),
                        InvoiceDate = DateTime.UtcNow,
                        TotalAmount = 8.50m,
                        TaxAmount = 1.42m,
                        PaymentMethod = PaymentMethod.Card,
                        PaymentStatus = PaymentStatus.Pending,
                        Status = InvoiceStatus.Draft,
                        CustomerId = admin.Id,
                        DueDate = DateTime.UtcNow.AddDays(30),
                        InvoiceType = "Standard",
                        CreatedById = admin.Id,
                        CreatedAt = DateTime.UtcNow
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
                            TaxAmount = 1.00m,
                            TaxType = TaxType.Standard
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
                            TaxAmount = 0.70m,
                            TaxType = TaxType.Standard
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
                            TaxAmount = 3.78m,
                            TaxType = TaxType.Standard
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
                            TaxAmount = 0.45m,
                            TaxType = TaxType.Reduced
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
                            TaxAmount = 1.40m,
                            TaxType = TaxType.Standard
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
                            TaxAmount = 0.02m,
                            TaxType = TaxType.Reduced
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
                        EntityName = "Demo User",
                        EntityId = "demo-entity",
                        OldValues = "{}",
                        NewValues = "{}",
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
                        EntityName = "Demo Invoice",
                        EntityId = "demo-invoice-1",
                        OldValues = "{}",
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
                        EntityName = "Demo Product",
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
                context.SystemConfigurations.Add(new SystemConfiguration());
            }

            await context.SaveChangesAsync();
        }
    }
} 
