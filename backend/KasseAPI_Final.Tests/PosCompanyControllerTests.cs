using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosCompanyControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("9c8f4e2b-1a3d-4f6e-8b7c-0d1e2f3a4b5c");

    private static (AppDbContext Db, ICurrentTenantAccessor TenantAccessor) CreateContext(Guid tenantId)
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosCompany_{Guid.NewGuid()}")
            .Options;
        return (new AppDbContext(options, tenantAccessor), tenantAccessor);
    }

    [Fact]
    public async Task GetCompanyInfo_WithoutTenant_Returns404()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(null);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosCompany_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        var controller = new PosCompanyController(db, tenantAccessor);

        var result = await controller.GetCompanyInfo(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetCompanyInfo_WhenNoSettingsRow_ReturnsEmptyDto()
    {
        var (db, tenantAccessor) = CreateContext(TenantId);
        await using (db)
        {
            var controller = new PosCompanyController(db, tenantAccessor);

            var result = await controller.GetCompanyInfo(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<PosCompanyInfoDto>(ok.Value);
            Assert.Equal(string.Empty, dto.CompanyName);
            Assert.Equal(string.Empty, dto.TaxNumber);
        }
    }

    [Fact]
    public async Task GetCompanyInfo_MapsCompanySettingsToDto()
    {
        var (db, tenantAccessor) = CreateContext(TenantId);
        await using (db)
        {
            db.CompanySettings.Add(new CompanySettings
            {
                TenantId = TenantId,
                CompanyName = "Cafe Wien GmbH",
                CompanyAddress = "Hauptstraße 1, 1010 Wien",
                CompanyTaxNumber = "ATU12345678",
                CompanyDescription = "Danke für Ihren Besuch!",
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
            });
            await db.SaveChangesAsync();

            var controller = new PosCompanyController(db, tenantAccessor);
            var result = await controller.GetCompanyInfo(CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<PosCompanyInfoDto>(ok.Value);
            Assert.Equal("Cafe Wien GmbH", dto.CompanyName);
            Assert.Equal("Hauptstraße 1, 1010 Wien", dto.CompanyAddress);
            Assert.Equal("ATU12345678", dto.TaxNumber);
            Assert.Equal("Danke für Ihren Besuch!", dto.ReceiptFooter);
            Assert.Equal("Europe/Vienna", dto.TimeZone);
            Assert.Equal(1, dto.WorkingHours.ReminderHoursBeforeClosing);
            Assert.False(dto.WorkingHours.Monday.IsClosed);
            Assert.Equal("22:00", dto.WorkingHours.Monday.CloseTime);
        }
    }
}
