using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Wave 0–1: tenant primitives and singleton settings scoping.</summary>
public class SettingsTenantWave1Tests
{
    [Fact]
    public void LegacyDefaultTenantIds_Are_Stable_For_Migration_Seed()
    {
        Assert.Equal(Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c"), LegacyDefaultTenantIds.Primary);
        Assert.Equal("default", LegacyDefaultTenantIds.PrimarySlug);
    }

    [Fact]
    public async Task SettingsTenantResolver_Returns_Legacy_Default_Id_When_No_Http_User()
    {
        var snapshotMock = new Mock<IAuthTenantSnapshotProvider>();
        snapshotMock
            .Setup(p => p.GetSnapshotAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTenantSnapshot(LegacyDefaultTenantIds.Primary.ToString("D"), "Default", LegacyDefaultTenantIds.PrimarySlug, null, null));
        var http = new Mock<IHttpContextAccessor>();
        http.Setup(h => h.HttpContext).Returns((HttpContext?)null);
        var resolver = new SettingsTenantResolver(http.Object, snapshotMock.Object);
        var id = await resolver.ResolveEffectiveTenantIdAsync();
        Assert.Equal(LegacyDefaultTenantIds.Primary, id);
    }

    [Fact]
    public async Task SystemSettings_Read_Scoped_By_TenantId_Returns_Expected_Row()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var db = new AppDbContext(options);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantA, Name = "Tenant A", Slug = "ta-" + tenantA.ToString("N")[..6] });
        db.Tenants.Add(new Tenant { Id = tenantB, Name = "Tenant B", Slug = "tb-" + tenantB.ToString("N")[..6] });

        db.SystemSettings.Add(MinimalSystemSettings(tenantA, "Alpha Co", "ATU11111111"));
        db.SystemSettings.Add(MinimalSystemSettings(tenantB, "Beta Co", "ATU22222222"));
        await db.SaveChangesAsync();

        var rowA = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantA);
        var rowB = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantB);

        Assert.NotNull(rowA);
        Assert.NotNull(rowB);
        Assert.Equal("Alpha Co", rowA!.CompanyName);
        Assert.Equal("Beta Co", rowB!.CompanyName);
    }

    [Fact]
    public async Task CompanySettings_Tenant_Scoped_Read_Does_Not_Cross_Tenants()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var db = new AppDbContext(options);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantA, Name = "A", Slug = "a-" + tenantA.ToString("N")[..6] });
        db.Tenants.Add(new Tenant { Id = tenantB, Name = "B", Slug = "b-" + tenantB.ToString("N")[..6] });
        db.CompanySettings.Add(MinimalCompanySettings(tenantA, "ATU10000001"));
        db.CompanySettings.Add(MinimalCompanySettings(tenantB, "ATU20000002"));
        await db.SaveChangesAsync();

        var onlyA = await db.CompanySettings.AsNoTracking().SingleOrDefaultAsync(c => c.TenantId == tenantA);
        var onlyB = await db.CompanySettings.AsNoTracking().SingleOrDefaultAsync(c => c.TenantId == tenantB);

        Assert.Equal("ATU10000001", onlyA!.CompanyTaxNumber);
        Assert.Equal("ATU20000002", onlyB!.CompanyTaxNumber);
    }

    private static SystemSettings MinimalSystemSettings(Guid tenantId, string companyName, string taxNumber) => new()
    {
        TenantId = tenantId,
        CompanyName = companyName,
        CompanyAddress = "Address",
        CompanyTaxNumber = taxNumber,
        DefaultLanguage = "de-DE",
        DefaultCurrency = "EUR",
        TimeZone = "Europe/Vienna",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm:ss",
        DecimalPlaces = 2,
        TaxRates = new Dictionary<string, decimal> { ["Standard"] = 20m },
    };

    private static CompanySettings MinimalCompanySettings(Guid tenantId, string taxNumber) => new()
    {
        TenantId = tenantId,
        CompanyName = "Co",
        CompanyAddress = "Addr",
        CompanyTaxNumber = taxNumber,
        BusinessHours = new Dictionary<string, string>(),
        Currency = "EUR",
        Language = "de-DE",
        TimeZone = "Europe/Vienna",
        DateFormat = "dd.MM.yyyy",
        TimeFormat = "HH:mm:ss",
        TaxCalculationMethod = "Standard",
        InvoiceNumbering = "Sequential",
        ReceiptNumbering = "Sequential",
        DefaultPaymentMethod = "Cash",
    };
}
