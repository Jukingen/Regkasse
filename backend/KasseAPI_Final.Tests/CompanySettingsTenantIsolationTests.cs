using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Tenant isolation for <see cref="CompanySettings"/> (schema, EF filter, API controllers).</summary>
public sealed class CompanySettingsTenantIsolationTests
{
    private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AppDbContext CreateContext(string databaseName, ICurrentTenantAccessor tenantAccessor)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor);
    }

    private static async Task SeedTwoTenantCompanySettingsAsync(AppDbContext db)
    {
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Tenant A", Slug = "tenant-a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantBId, Name = "Tenant B", Slug = "tenant-b", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.CompanySettings.AddRange(
            MinimalCompanySettings(TenantAId, "Tenant A GmbH", "ATU11111111"),
            MinimalCompanySettings(TenantBId, "Tenant B GmbH", "ATU22222222"));
        await db.SaveChangesAsync();
    }

    private static CompanySettings MinimalCompanySettings(Guid tenantId, string name, string taxNumber) => new()
    {
        TenantId = tenantId,
        CompanyName = name,
        CompanyAddress = "Address",
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

    private static CompanySettingsController CreateCompanySettingsController(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor)
    {
        var controller = new CompanySettingsController(
            db,
            Mock.Of<ILogger<CompanySettingsController>>(),
            tenantAccessor,
            Mock.Of<IAuditLogService>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task EfGlobalQueryFilter_WithTenantAccessor_ReturnsOnlyScopedRows()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using (var seedDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null)))
        {
            await SeedTwoTenantCompanySettingsAsync(seedDb);
        }

        await using var tenantADb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var visible = await tenantADb.CompanySettings.AsNoTracking().ToListAsync();

        Assert.Single(visible);
        Assert.Equal(TenantAId, visible[0].TenantId);
        Assert.Equal("ATU11111111", visible[0].CompanyTaxNumber);
    }

    [Fact]
    public async Task EfGlobalQueryFilter_WithoutTenantAccessor_IsFailClosed()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using (var seedDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null)))
        {
            await SeedTwoTenantCompanySettingsAsync(seedDb);
        }

        await using var unscopedDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        var visible = await unscopedDb.CompanySettings.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
        Assert.Equal(2, await unscopedDb.CompanySettings.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task GetCompanySettings_AsTenantA_ReturnsOnlyTenantAData()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        await SeedTwoTenantCompanySettingsAsync(db);

        var controller = CreateCompanySettingsController(db, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var result = await controller.GetCompanySettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var settings = Assert.IsType<CompanySettings>(ok.Value);
        Assert.Equal(TenantAId, settings.TenantId);
        Assert.Equal("Tenant A GmbH", settings.CompanyName);
        Assert.Equal("ATU11111111", settings.CompanyTaxNumber);
        Assert.DoesNotContain("Tenant B", settings.CompanyName);
        Assert.NotEqual("ATU22222222", settings.CompanyTaxNumber);
    }

    [Fact]
    public async Task GetCompanySettings_AsTenantB_DoesNotExposeTenantAData()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantBId));
        await SeedTwoTenantCompanySettingsAsync(db);

        var controller = CreateCompanySettingsController(db, TenantTestDoubles.TenantAccessorReturning(TenantBId));
        var result = await controller.GetCompanySettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var settings = Assert.IsType<CompanySettings>(ok.Value);
        Assert.Equal(TenantBId, settings.TenantId);
        Assert.Equal("Tenant B GmbH", settings.CompanyName);
        Assert.Equal("ATU22222222", settings.CompanyTaxNumber);
        Assert.DoesNotContain("Tenant A", settings.CompanyName);
        Assert.NotEqual("ATU11111111", settings.CompanyTaxNumber);
    }

    [Fact]
    public async Task WhenTenantAUpdates_TenantBStillSeesOwnData_NotTenantAChanges()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using (var seedDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null)))
        {
            await SeedTwoTenantCompanySettingsAsync(seedDb);
        }

        var updateRequest = new UpdateCompanySettingsRequest
        {
            CompanyName = "Updated A GmbH",
            CompanyAddress = "A Updated Street 99",
            CompanyTaxNumber = "ATU99999999",
            DefaultCurrency = "EUR",
            DefaultLanguage = "de-DE",
            DefaultTimeZone = "Europe/Vienna",
            DefaultDateFormat = "dd.MM.yyyy",
            DefaultTimeFormat = "HH:mm:ss",
            DefaultDecimalPlaces = 2,
            DefaultPaymentMethod = "Cash",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            BusinessHours = new Dictionary<string, string>(),
        };

        await using (var dbA = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId)))
        {
            var controllerA = CreateCompanySettingsController(dbA, TenantTestDoubles.TenantAccessorReturning(TenantAId));
            var putResult = await controllerA.UpdateCompanySettings(updateRequest);
            var putOk = Assert.IsAssignableFrom<ObjectResult>(putResult);
            Assert.Equal(StatusCodes.Status200OK, putOk.StatusCode);
        }

        await using (var dbB = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantBId)))
        {
            var controllerB = CreateCompanySettingsController(dbB, TenantTestDoubles.TenantAccessorReturning(TenantBId));
            var getB = await controllerB.GetCompanySettings();
            var okB = Assert.IsType<OkObjectResult>(getB.Result);
            var settingsB = Assert.IsType<CompanySettings>(okB.Value);

            Assert.Equal("Tenant B GmbH", settingsB.CompanyName);
            Assert.Equal("ATU22222222", settingsB.CompanyTaxNumber);
            Assert.NotEqual("Updated A GmbH", settingsB.CompanyName);
            Assert.NotEqual("ATU99999999", settingsB.CompanyTaxNumber);

            var posB = new PosCompanyController(dbB, TenantTestDoubles.TenantAccessorReturning(TenantBId));
            var posResult = await posB.GetCompanyInfo(CancellationToken.None);
            var posOk = Assert.IsType<OkObjectResult>(posResult.Result);
            var posDto = Assert.IsType<PosCompanyInfoDto>(posOk.Value);
            Assert.Equal("Tenant B GmbH", posDto.CompanyName);
            Assert.Equal("ATU22222222", posDto.TaxNumber);
        }

        await using var verifyDb = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        var rows = await verifyDb.CompanySettings.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        var rowA = rows.Single(r => r.TenantId == TenantAId);
        var rowB = rows.Single(r => r.TenantId == TenantBId);
        Assert.Equal("Updated A GmbH", rowA.CompanyName);
        Assert.Equal("ATU99999999", rowA.CompanyTaxNumber);
        Assert.Equal("Tenant B GmbH", rowB.CompanyName);
        Assert.Equal("ATU22222222", rowB.CompanyTaxNumber);
    }

    [Fact]
    public async Task UpdateCompanySettings_CreatesRowUnderEffectiveTenantOnly()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Tenant A", Slug = "tenant-a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { Id = TenantBId, Name = "Tenant B", Slug = "tenant-b", IsActive = true, CreatedAt = DateTime.UtcNow });
        db.CompanySettings.Add(MinimalCompanySettings(TenantBId, "Tenant B GmbH", "ATU22222222"));
        await db.SaveChangesAsync();

        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(TenantAId);
        var controller = CreateCompanySettingsController(db, tenantAccessor);
        var putResult = await controller.UpdateCompanySettings(new UpdateCompanySettingsRequest
        {
            CompanyName = "New A GmbH",
            CompanyAddress = "A Street 1",
            CompanyTaxNumber = "ATU33333333",
            DefaultCurrency = "EUR",
            DefaultLanguage = "de-DE",
            DefaultTimeZone = "Europe/Vienna",
            DefaultDateFormat = "dd.MM.yyyy",
            DefaultTimeFormat = "HH:mm:ss",
            DefaultDecimalPlaces = 2,
            DefaultPaymentMethod = "Cash",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            BusinessHours = new Dictionary<string, string>(),
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(putResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        var rows = await db.CompanySettings.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);

        var rowA = rows.Single(r => r.TenantId == TenantAId);
        var rowB = rows.Single(r => r.TenantId == TenantBId);
        Assert.Equal("New A GmbH", rowA.CompanyName);
        Assert.Equal("ATU33333333", rowA.CompanyTaxNumber);
        Assert.Equal("Tenant B GmbH", rowB.CompanyName);
        Assert.Equal("ATU22222222", rowB.CompanyTaxNumber);
    }

    [Fact]
    public async Task PosCompany_GetCompanyInfo_DoesNotReturnOtherTenantSettings()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        await SeedTwoTenantCompanySettingsAsync(db);

        var controller = new PosCompanyController(db, TenantTestDoubles.TenantAccessorReturning(TenantAId));
        var result = await controller.GetCompanyInfo(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PosCompanyInfoDto>(ok.Value);
        Assert.Equal("Tenant A GmbH", dto.CompanyName);
        Assert.Equal("ATU11111111", dto.TaxNumber);
        Assert.NotEqual("Tenant B GmbH", dto.CompanyName);
    }

    [Fact]
    public async Task GetCompanySettings_WithoutTenant_Returns404()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        await SeedTwoTenantCompanySettingsAsync(db);

        var controller = CreateCompanySettingsController(db, TenantTestDoubles.TenantAccessorReturning(null));
        var result = await controller.GetCompanySettings();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task PosCompany_GetCompanyInfo_WithoutTenant_Returns404()
    {
        var dbName = $"CompanySettingsIso_{Guid.NewGuid():N}";
        await using var db = CreateContext(dbName, TenantTestDoubles.TenantAccessorReturning(null));
        await SeedTwoTenantCompanySettingsAsync(db);

        var controller = new PosCompanyController(db, TenantTestDoubles.TenantAccessorReturning(null));
        var result = await controller.GetCompanyInfo(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }
}
