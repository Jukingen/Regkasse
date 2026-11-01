using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Seed;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantDemoSeederTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantDemoSeeder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task SeedAsync_WithoutProfile_SeedsGastronomyOnly_AndIsIdempotent()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo-test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeder = new TenantDemoSeeder(db);
        var first = await seeder.SeedAsync(tenantId);

        Assert.Equal(4, first.ProductsAdded);
        Assert.Equal(2, first.CustomersAdded);
        Assert.NotNull(first.CashRegisterId);
        Assert.Null(first.FailedProfile);
        Assert.Equal(VerticalProfileIds.Gastronomy, await seeder.ResolveVerticalProfileIdAsync(tenantId));

        var keys = await db.Categories.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.Key)
            .ToListAsync();
        Assert.Equal(new[] { VerticalProfileIds.Gastronomy }, keys);
        Assert.DoesNotContain(VerticalProfileIds.Vet, keys);
        Assert.DoesNotContain(VerticalProfileIds.Doctor, keys);
        Assert.DoesNotContain(VerticalProfileIds.HairSalon, keys);

        var productNames = await db.Products.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive)
            .Select(row => row.Name)
            .ToListAsync();
        Assert.Equal(4, productNames.Count);
        Assert.Contains("Wiener Schnitzel", productNames);
        Assert.DoesNotContain("Ordination", productNames);
        Assert.DoesNotContain("Impfung", productNames);
        Assert.Equal(2, await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId && row.IsActive));

        await DemoVerticalSeedAssertions.AssertProfileAsync(db, tenantId, VerticalProfileIds.Gastronomy);

        var register = await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.TenantId == tenantId);
        Assert.Equal(first.CashRegisterId, register.Id);
        Assert.Equal(CashRegisterNumbers.ForTenant(tenantId, "demo-test"), register.RegisterNumber);
        Assert.NotEqual(DevTenantSeeder.RegisterNumber, register.RegisterNumber);
        Assert.Equal(RegisterStatus.Closed, register.Status);
        Assert.Equal(
            0,
            await db.CashierShifts.IgnoreQueryFilters().CountAsync(row =>
                row.CashRegisterId == register.Id && row.Status == CashierShiftStatuses.Active && row.EndedAt == null));

        var second = await seeder.SeedAsync(tenantId);
        Assert.Equal(0, second.ProductsAdded);
        Assert.Equal(0, second.CustomersAdded);
        Assert.Equal(register.Id, second.CashRegisterId);
        Assert.Equal(first.ProductsAdded, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(first.CustomersAdded, await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(1, await db.CashRegisters.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Null(second.FailedProfile);
        await DemoVerticalSeedAssertions.AssertProfileAsync(db, tenantId, VerticalProfileIds.Gastronomy);
    }

    [Fact]
    public async Task SeedAsync_UsesCompanySettingsVerticalProfile()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Arzt",
            Slug = "praxis",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            VerticalProfileId = VerticalProfileIds.Doctor,
            CompanyName = "Arztpraxis",
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
        await db.SaveChangesAsync();

        var seeder = new TenantDemoSeeder(db);
        var seeded = await seeder.SeedAsync(tenantId);

        Assert.Equal(VerticalProfileIds.Doctor, await seeder.ResolveVerticalProfileIdAsync(tenantId));
        Assert.Equal(4, seeded.ProductsAdded);
        Assert.Equal(2, seeded.CustomersAdded);
        Assert.Null(seeded.FailedProfile);

        var keys = await db.Categories.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive)
            .Select(row => row.Key)
            .ToListAsync();
        Assert.Equal(new[] { VerticalProfileIds.Doctor }, keys);

        var names = await db.Products.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.IsActive)
            .Select(row => row.Name)
            .ToListAsync();
        Assert.Contains("Ordination", names);
        Assert.Contains("Blutabnahme", names);
        Assert.DoesNotContain("Impfung", names);
        Assert.DoesNotContain("Haarschnitt", names);
        Assert.DoesNotContain("Wiener Schnitzel", names);
        await DemoVerticalSeedAssertions.AssertProfileAsync(db, tenantId, VerticalProfileIds.Doctor);
    }

    [Fact]
    public async Task SeedAsync_MatchesExistingBarcodesAndCustomerNumbers()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo-existing",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeder = new TenantDemoSeeder(db);
        await seeder.SeedAsync(tenantId);
        var productsBefore = await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId);
        var customersBefore = await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId);

        var again = await TenantDemoSeeder.SeedTenantAsync(db, tenantId);

        Assert.Equal(0, again.ProductsAdded);
        Assert.Equal(0, again.CustomersAdded);
        Assert.Equal(productsBefore, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(customersBefore, await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Null(again.FailedProfile);
        Assert.Equal(new[] { VerticalProfileIds.Gastronomy }, await db.Categories.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId)
            .Select(row => row.Key)
            .ToListAsync());
        await DemoVerticalSeedAssertions.AssertProfileAsync(db, tenantId, VerticalProfileIds.Gastronomy);
    }
}
