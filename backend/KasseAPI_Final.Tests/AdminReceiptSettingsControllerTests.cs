using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminReceiptSettingsControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("c1b2c3d4-e5f6-7890-abcd-ef1234567890");

    private static (AppDbContext Db, AdminReceiptSettingsController Controller) Create(Guid? tenantId)
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptSettings_{Guid.NewGuid()}")
            .Options;
        var db = new AppDbContext(options, tenantAccessor);
        var controller = new AdminReceiptSettingsController(
            db,
            tenantAccessor,
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminReceiptSettingsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return (db, controller);
    }

    [Fact]
    public async Task GetReceiptSettings_WithoutTenant_Returns404()
    {
        var (db, controller) = Create(null);
        await using (db)
        {
            var result = await controller.GetReceiptSettings(CancellationToken.None);
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }

    [Fact]
    public async Task GetReceiptSettings_WhenNoRow_ReturnsDefaultMessage()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            var result = await controller.GetReceiptSettings(CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ReceiptSettingsDto>(ok.Value);
            Assert.Null(dto.ThankYouMessage);
            Assert.Equal(ReceiptThankYouMessage.Default, dto.EffectiveThankYouMessage);
            Assert.Equal(ReceiptThankYouMessage.Default, dto.DefaultThankYouMessage);
            Assert.Null(dto.CompanyDescription);
        }
    }

    [Fact]
    public async Task UpdateReceiptSettings_PersistsCustomMessage()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            var post = await controller.UpdateReceiptSettings(
                new UpdateReceiptSettingsRequest { ThankYouMessage = "  Danke schön!  " },
                CancellationToken.None);

            var postOk = Assert.IsType<OkObjectResult>(post.Result);
            var postDto = Assert.IsType<ReceiptSettingsDto>(postOk.Value);
            Assert.Equal("Danke schön!", postDto.ThankYouMessage);
            Assert.Equal("Danke schön!", postDto.EffectiveThankYouMessage);

            var get = await controller.GetReceiptSettings(CancellationToken.None);
            var getOk = Assert.IsType<OkObjectResult>(get.Result);
            var getDto = Assert.IsType<ReceiptSettingsDto>(getOk.Value);
            Assert.Equal("Danke schön!", getDto.ThankYouMessage);
        }
    }

    [Fact]
    public async Task UpdateReceiptSettings_EmptyString_UsesDefault()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            await controller.UpdateReceiptSettings(
                new UpdateReceiptSettingsRequest { ThankYouMessage = "Custom" },
                CancellationToken.None);

            var cleared = await controller.UpdateReceiptSettings(
                new UpdateReceiptSettingsRequest { ThankYouMessage = "   " },
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(cleared.Result);
            var dto = Assert.IsType<ReceiptSettingsDto>(ok.Value);
            Assert.Null(dto.ThankYouMessage);
            Assert.Equal(ReceiptThankYouMessage.Default, dto.EffectiveThankYouMessage);
        }
    }

    [Fact]
    public async Task GetReceiptSettings_DoesNotUseCompanyDescriptionAsThankYou()
    {
        var (db, controller) = Create(TenantId);
        await using (db)
        {
            db.CompanySettings.Add(new CompanySettings
            {
                TenantId = TenantId,
                CompanyName = "Cafe",
                CompanyAddress = "Wien",
                CompanyTaxNumber = "ATU12345678",
                CompanyDescription = "Willkommen",
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

            var result = await controller.GetReceiptSettings(CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ReceiptSettingsDto>(ok.Value);
            Assert.Null(dto.ThankYouMessage);
            Assert.Equal(ReceiptThankYouMessage.Default, dto.EffectiveThankYouMessage);
            Assert.Equal("Willkommen", dto.CompanyDescription);
        }
    }
}
