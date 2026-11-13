using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Seed;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevTenantSeederTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DevTenantSeeder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public void CorrectedDemoUserEmail_RewritesMismatchedTenantHost_AndKeepsMatchingAddresses()
    {
        Assert.Equal(
            "cashier1@dev.regkasse.at",
            DevTenantSeeder.CorrectedDemoUserEmail("cashier1", "pos@dev.regkasse.at", "dev"));
        Assert.Equal(
            "chef1@dev.regkasse.at",
            DevTenantSeeder.CorrectedDemoUserEmail("chef1", null, "dev"));
        Assert.Null(DevTenantSeeder.CorrectedDemoUserEmail("chef1", "chef1@dev.regkasse.at", "dev"));
        Assert.Equal(
            "manager1@dev.regkasse.at",
            DevTenantSeeder.CorrectedDemoUserEmail("manager1", "pos@dev2.regkasse.at", "dev"));
        Assert.Null(DevTenantSeeder.CorrectedDemoUserEmail("demo@demo.com", "demo@demo.com", "dev"));
        Assert.Null(DevTenantSeeder.CorrectedDemoUserEmail("manager1", "manager1@example.com", "prod"));
    }

    [Fact]
    public async Task SeedAsync_GivesEachDevTenantOnlyItsOwnVertical_AndLeavesTheCashRegisterClosed()
    {
        await using var db = CreateDb();
        var doctorTenantId = Guid.Parse("b0000001-0001-4001-8001-00000000000d");
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = doctorTenantId,
            Name = "Arztpraxis",
            Slug = "dev-doctor",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = doctorTenantId,
            VerticalProfileId = VerticalProfileIds.Doctor,
            CompanyName = "Arztpraxis Demo",
            CompanyAddress = "Wien 1",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            Currency = "EUR",
            Country = "AT",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });

        var foreignCategory = new Category
        {
            TenantId = DemoTenantIds.Dev,
            Key = VerticalProfileIds.Vet,
            Name = "Tierarzt",
            VatRate = 20m,
            FiscalCategory = RksvProductCategory.Other,
            IsActive = true,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        var genericCategory = new Category
        {
            TenantId = doctorTenantId,
            Key = DevTenantSeeder.GenericCategoryKey,
            Name = "Allgemein",
            VatRate = 20m,
            FiscalCategory = RksvProductCategory.Other,
            IsActive = true,
        };
        db.Categories.AddRange(foreignCategory, genericCategory);
        await db.SaveChangesAsync();

        db.Products.Add(new Product
        {
            TenantId = DemoTenantIds.Dev,
            Name = "Impfung",
            Price = 9.90m,
            TaxType = (int)TaxType.Standard,
            Category = "Tierarzt",
            CategoryId = foreignCategory.Id,
            TaxGroupId = Guid.NewGuid(),
            Barcode = DevTenantSeeder.Barcode(VerticalProfileIds.Vet, 1),
            Unit = "Stk",
            IsActive = true,
            CreatedBy = DevTenantSeeder.CreatedBy,
        });
        db.Products.Add(new Product
        {
            TenantId = doctorTenantId,
            Name = "Demo Produkt 1",
            Price = 9.99m,
            TaxType = (int)TaxType.Standard,
            Category = "Allgemein",
            CategoryId = genericCategory.Id,
            TaxGroupId = Guid.NewGuid(),
            Unit = "Stk",
            IsActive = true,
            CreatedBy = "doctor-demo-seed",
        });
        await db.SaveChangesAsync();

        await DevTenantSeeder.SeedAsync(db);

        var devKeys = await ActiveCategoryKeysAsync(db, DemoTenantIds.Dev);
        Assert.Equal(new[] { VerticalProfileIds.GastronomyTables }, devKeys);
        Assert.DoesNotContain(VerticalProfileIds.Gastronomy, devKeys);
        Assert.DoesNotContain(VerticalProfileIds.Vet, devKeys);
        Assert.DoesNotContain(VerticalProfileIds.Doctor, devKeys);
        Assert.DoesNotContain(VerticalProfileIds.HairSalon, devKeys);

        var tablesId = await db.Categories.IgnoreQueryFilters()
            .Where(row => row.TenantId == DemoTenantIds.Dev && row.Key == VerticalProfileIds.GastronomyTables && row.IsActive)
            .Select(row => row.Id)
            .SingleAsync();
        var tableProducts = await db.Products.IgnoreQueryFilters()
            .Where(row => row.TenantId == DemoTenantIds.Dev && row.CategoryId == tablesId && row.IsActive)
            .Select(row => row.Name)
            .ToListAsync();
        Assert.Equal(4, tableProducts.Count);
        Assert.Contains("Tagesmenü", tableProducts);
        Assert.False(await db.Categories.IgnoreQueryFilters().AnyAsync(row => row.Id == foreignCategory.Id));
        Assert.False(await db.Products.IgnoreQueryFilters().AnyAsync(row =>
            row.TenantId == DemoTenantIds.Dev && row.Barcode == DevTenantSeeder.Barcode(VerticalProfileIds.Vet, 1)));

        var doctorKeys = await ActiveCategoryKeysAsync(db, doctorTenantId);
        Assert.Equal(new[] { VerticalProfileIds.Doctor }, doctorKeys);
        Assert.False(await db.Categories.IgnoreQueryFilters().AnyAsync(row => row.Key == DevTenantSeeder.GenericCategoryKey));
        Assert.False(await db.Products.IgnoreQueryFilters().AnyAsync(row =>
            row.TenantId == doctorTenantId && row.Name == "Demo Produkt 1"));
        var doctorProducts = await db.Products.IgnoreQueryFilters()
            .Where(row => row.TenantId == doctorTenantId && row.IsActive)
            .Select(row => row.Name)
            .ToListAsync();
        Assert.Contains("Ordination", doctorProducts);
        Assert.Contains("Blutabnahme", doctorProducts);
        Assert.DoesNotContain("Impfung", doctorProducts);
        Assert.DoesNotContain("Haarschnitt", doctorProducts);

        await DemoVerticalSeedAssertions.AssertProfileAsync(db, DemoTenantIds.Dev, VerticalProfileIds.GastronomyTables);
        await DemoVerticalSeedAssertions.AssertProfileAsync(db, doctorTenantId, VerticalProfileIds.Doctor);
        Assert.Equal(
            VerticalProfileIds.GastronomyTables,
            await db.CompanySettings.IgnoreQueryFilters()
                .Where(row => row.TenantId == DemoTenantIds.Dev)
                .Select(row => row.VerticalProfileId)
                .SingleAsync());

        var register = await db.CashRegisters.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == DemoTenantIds.Dev);
        Assert.Equal(RegisterStatus.Closed, register.Status);
        Assert.True(register.IsActive);
        Assert.Equal(
            0,
            await db.CashierShifts.IgnoreQueryFilters().CountAsync(row =>
                row.CashRegisterId == register.Id && row.Status == CashierShiftStatuses.Active));

        var devProductsBefore = await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == DemoTenantIds.Dev);
        var doctorProductsBefore = await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == doctorTenantId);
        await DevTenantSeeder.SeedAsync(db);
        Assert.Equal(devProductsBefore, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == DemoTenantIds.Dev));
        Assert.Equal(doctorProductsBefore, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == doctorTenantId));
        Assert.Equal(RegisterStatus.Closed, (await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.TenantId == DemoTenantIds.Dev)).Status);
        Assert.Equal(new[] { VerticalProfileIds.GastronomyTables }, await ActiveCategoryKeysAsync(db, DemoTenantIds.Dev));
        Assert.Equal(new[] { VerticalProfileIds.Doctor }, await ActiveCategoryKeysAsync(db, doctorTenantId));
    }

    [Fact]
    public async Task SeedAsync_ClosesSeedOpenedRegister_AndEndsDemoAdminShift()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var register = new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = DemoTenantIds.Dev,
            RegisterNumber = DevTenantSeeder.RegisterNumber,
            Location = "Hauptkasse",
            Status = RegisterStatus.Open,
            IsDefaultForTenant = true,
            IsActive = true,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        db.CashRegisters.Add(register);
        db.CashierShifts.Add(new CashierShift
        {
            TenantId = DemoTenantIds.Dev,
            CashRegisterId = register.Id,
            CashierId = DemoTenantAdminUserIds.Dev,
            CashierName = "Demo Kasse",
            Status = CashierShiftStatuses.Active,
            IsActive = true,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = DevTenantSeeder.CreatedBy,
        });
        await db.SaveChangesAsync();

        await DevTenantSeeder.SeedAsync(db);

        var stored = await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.Id == register.Id);
        Assert.Equal(RegisterStatus.Closed, stored.Status);
        Assert.Null(stored.CurrentUserId);
        var shift = await db.CashierShifts.IgnoreQueryFilters().SingleAsync(row => row.CashRegisterId == register.Id);
        Assert.Equal(CashierShiftStatuses.Completed, shift.Status);
        Assert.NotNull(shift.EndedAt);
        Assert.Equal(
            VerticalProfileIds.GastronomyTables,
            await db.CompanySettings.IgnoreQueryFilters()
                .Where(row => row.TenantId == DemoTenantIds.Dev)
                .Select(row => row.VerticalProfileId)
                .SingleAsync());
    }

    [Fact]
    public async Task SeedAsync_ClosesDemoAdminShiftOnNonDefaultRegister_AndKeepsCashierRegister()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var closedDefault = new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = DemoTenantIds.Dev,
            RegisterNumber = "KASSE-003",
            Location = "Nebenkasse",
            Status = RegisterStatus.Closed,
            IsDefaultForTenant = true,
            IsActive = true,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        var seedOpened = new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = DemoTenantIds.Dev,
            RegisterNumber = "KASSE-002",
            Location = "Saal",
            Status = RegisterStatus.Open,
            CurrentUserId = null,
            IsDefaultForTenant = false,
            IsActive = true,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        var cashierRegister = new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = DemoTenantIds.Dev,
            RegisterNumber = "KASSE-004",
            Location = "Theke",
            Status = RegisterStatus.Open,
            CurrentUserId = "cashier1",
            IsDefaultForTenant = false,
            IsActive = true,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        db.CashRegisters.AddRange(closedDefault, seedOpened, cashierRegister);
        db.CashierShifts.AddRange(
            new CashierShift
            {
                TenantId = DemoTenantIds.Dev,
                CashRegisterId = seedOpened.Id,
                CashierId = DemoTenantAdminUserIds.Dev,
                CashierName = "Demo Kasse",
                Status = CashierShiftStatuses.Active,
                IsActive = true,
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = DevTenantSeeder.CreatedBy,
            },
            new CashierShift
            {
                TenantId = DemoTenantIds.Dev,
                CashRegisterId = cashierRegister.Id,
                CashierId = "cashier1",
                CashierName = "Cashier",
                Status = CashierShiftStatuses.Active,
                IsActive = true,
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = DevTenantSeeder.CreatedBy,
            });
        await db.SaveChangesAsync();

        await DevTenantSeeder.SeedAsync(db);

        var released = await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.Id == seedOpened.Id);
        Assert.Equal(RegisterStatus.Closed, released.Status);
        Assert.Null(released.CurrentUserId);
        var seedShift = await db.CashierShifts.IgnoreQueryFilters().SingleAsync(row => row.CashRegisterId == seedOpened.Id);
        Assert.Equal(CashierShiftStatuses.Completed, seedShift.Status);
        Assert.NotNull(seedShift.EndedAt);

        var kept = await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.Id == cashierRegister.Id);
        Assert.Equal(RegisterStatus.Open, kept.Status);
        Assert.Equal("cashier1", kept.CurrentUserId);
        Assert.Equal(
            CashierShiftStatuses.Active,
            await db.CashierShifts.IgnoreQueryFilters()
                .Where(row => row.CashRegisterId == cashierRegister.Id)
                .Select(row => row.Status)
                .SingleAsync());
    }

    [Fact]
    public async Task SeedAsync_KeepsPaymentReferencedDemoProduct_AndDoesNotChangeThePayment()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var category = new Category
        {
            TenantId = DemoTenantIds.Dev,
            Key = VerticalProfileIds.Vet,
            Name = "Tierarzt",
            VatRate = 20m,
            FiscalCategory = RksvProductCategory.Other,
            IsActive = true,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            TenantId = DemoTenantIds.Dev,
            Name = "Impfung",
            Price = 9.90m,
            TaxType = (int)TaxType.Standard,
            Category = "Tierarzt",
            CategoryId = category.Id,
            TaxGroupId = Guid.NewGuid(),
            Barcode = DevTenantSeeder.Barcode(VerticalProfileIds.Vet, 1),
            Unit = "Stk",
            IsActive = true,
            CreatedBy = DevTenantSeeder.CreatedBy,
        };
        db.Products.Add(product);
        var register = new CashRegister
        {
            TenantId = DemoTenantIds.Dev,
            RegisterNumber = "KASSE-PAY",
            Location = "Test",
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.CashRegisters.Add(register);
        var payment = new PaymentDetails
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = "Test",
            CashierId = "cashier1",
            CashRegisterId = register.Id,
            TotalAmount = 9.90m,
            TaxAmount = 1.65m,
            PaymentMethodRaw = "Cash",
            TseSignature = "test-signature",
            ReceiptNumber = "DEV-KEEP-1",
            PaymentItems = JsonDocument.Parse($"[{{\"productId\":\"{product.Id}\"}}]"),
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true,
        };
        db.PaymentDetails.Add(payment);
        await db.SaveChangesAsync();

        await DevTenantSeeder.SeedAsync(db);

        var kept = await db.Products.IgnoreQueryFilters().SingleAsync(row => row.Id == product.Id);
        Assert.False(kept.IsActive);
        var keptCategory = await db.Categories.IgnoreQueryFilters().SingleAsync(row => row.Id == category.Id);
        Assert.False(keptCategory.IsActive);
        var stored = await db.PaymentDetails.IgnoreQueryFilters().SingleAsync(row => row.Id == payment.Id);
        Assert.Equal("DEV-KEEP-1", stored.ReceiptNumber);
        Assert.Equal(0, await db.Receipts.IgnoreQueryFilters().CountAsync());
        Assert.Equal(0, await db.TseSignatures.IgnoreQueryFilters().CountAsync());
    }

    private static async Task<string[]> ActiveCategoryKeysAsync(AppDbContext db, Guid tenantId)
    {
        var catalogKeys = DevTenantSeeder.Catalog.Select(row => row.ProfileId).ToHashSet(StringComparer.Ordinal);
        var keys = await db.Categories.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive && catalogKeys.Contains(row.Key))
            .Select(row => row.Key)
            .ToListAsync();
        return keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
