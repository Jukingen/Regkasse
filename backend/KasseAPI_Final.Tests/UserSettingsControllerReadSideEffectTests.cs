using System.Security.Claims;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

public class UserSettingsControllerReadSideEffectTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UsrSetRead_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static UserSettingsController CreateController(AppDbContext ctx, string userId)
    {
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var c = new UserSettingsController(ctx, Mock.Of<ILogger<UserSettingsController>>(), resolution);
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
                    "Test")),
            },
        };
        return c;
    }

    [Fact]
    public async Task GetUserSettings_DoesNotPersistSoleOpenAssignment_WhenEligible()
    {
        await using var ctx = CreateContext();
        const string userId = "u1";
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CashRegisterId = null,
            Language = "de-DE",
            Currency = "EUR",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            DefaultTaxRate = 20,
            EnableDiscounts = true,
            EnableCoupons = true,
            AutoPrintReceipts = false,
            ReceiptHeader = "h",
            ReceiptFooter = "f",
            FinanzOnlineEnabled = false,
            SessionTimeout = 30,
            RequirePinForRefunds = true,
            MaxDiscountPercentage = 50,
            Theme = "light",
            CompactMode = false,
            ShowProductImages = true,
            EnableNotifications = true,
            LowStockAlert = true,
            DefaultPaymentMethod = "mixed",
            DefaultTableNumber = "1",
            DefaultWaiterName = "K",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, userId);
        var getResult = await controller.GetUserSettings();
        Assert.IsType<OkObjectResult>(getResult.Result);

        var us = await ctx.UserSettings.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Assert.Null(us.CashRegisterId);
    }

    [Fact]
    public async Task BootstrapUserSettings_PersistsSoleOpenAssignment_WhenEligible()
    {
        await using var ctx = CreateContext();
        const string userId = "u1";
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CashRegisterId = null,
            Language = "de-DE",
            Currency = "EUR",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            DefaultTaxRate = 20,
            EnableDiscounts = true,
            EnableCoupons = true,
            AutoPrintReceipts = false,
            ReceiptHeader = "h",
            ReceiptFooter = "f",
            FinanzOnlineEnabled = false,
            SessionTimeout = 30,
            RequirePinForRefunds = true,
            MaxDiscountPercentage = 50,
            Theme = "light",
            CompactMode = false,
            ShowProductImages = true,
            EnableNotifications = true,
            LowStockAlert = true,
            DefaultPaymentMethod = "mixed",
            DefaultTableNumber = "1",
            DefaultWaiterName = "K",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, userId);
        var postResult = await controller.BootstrapUserSettings();
        Assert.IsType<OkObjectResult>(postResult.Result);

        var us = await ctx.UserSettings.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Assert.Equal(regId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task GetUserSettings_StillCreatesDefaultRow_WhenMissing()
    {
        await using var ctx = CreateContext();
        const string userId = "new-user";

        var controller = CreateController(ctx, userId);
        var getResult = await controller.GetUserSettings();
        Assert.IsType<OkObjectResult>(getResult.Result);

        Assert.Equal(1, await ctx.UserSettings.CountAsync(s => s.UserId == userId));
    }
}
