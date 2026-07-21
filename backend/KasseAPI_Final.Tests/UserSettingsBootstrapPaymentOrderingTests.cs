using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
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

/// <summary>
/// Documents interaction between read-only GET settings (no sole-assign), explicit bootstrap, and payment pre-check —
/// not snapshot-only: order of HTTP calls changes persisted <see cref="UserSettings.CashRegisterId"/>.
/// </summary>
public class UserSettingsBootstrapPaymentOrderingTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UsrBootOrder_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static UserSettingsController CreateController(AppDbContext ctx, string userId)
    {
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
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

    /// <summary>
    /// GET no longer sole-assigns: after GET, payment pre-check still OK for sole operational + own shift (no settings id).
    /// POST bootstrap then persists assignment for clients that rely on profile id.
    /// </summary>
    [Fact]
    public async Task TemporalOrder_GetDoesNotAssign_BootstrapAssigns_ThenValidatePaymentMatchesBootstrap()
    {
        await using var ctx = CreateContext();
        const string userId = "u-pos";
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
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = Guid.NewGuid(),
            RegisterNumber = "Z",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, userId);

        var getResult = await controller.GetUserSettings();
        Assert.IsType<OkObjectResult>(getResult.Result);
        var afterGet = await ctx.UserSettings.AsNoTracking().SingleAsync(us => us.UserId == userId);
        Assert.Null(afterGet.CashRegisterId);

        var resolutionAfterGet = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var preAfterGet = await resolutionAfterGet.ValidatePaymentRegisterAsync(userId, regId, new ClaimsPrincipal());
        Assert.True(preAfterGet.Ok);

        var bootResult = await controller.BootstrapUserSettings();
        Assert.IsType<OkObjectResult>(bootResult.Result);
        var afterBoot = await ctx.UserSettings.AsNoTracking().SingleAsync(us => us.UserId == userId);
        Assert.Equal(regId.ToString(), afterBoot.CashRegisterId);

        var resolutionFinal = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var preFinal = await resolutionFinal.ValidatePaymentRegisterAsync(userId, regId, new ClaimsPrincipal());
        Assert.True(preFinal.Ok);
    }

    /// <summary>
    /// Selectable list does not depend on GET having run sole-assign; sole operational + one open row still lists for CashRegisterView.
    /// </summary>
    [Fact]
    public async Task ListSelectable_AfterGetOnly_StillReturnsOperationalOpen_WhenSecondRowDormant()
    {
        await using var ctx = CreateContext();
        const string userId = "u-list";
        var openId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = openId,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = Guid.NewGuid(),
            RegisterNumber = "D",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(UserSettingsBootstrap.CreateDefaultRow(userId));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, userId);
        await controller.GetUserSettings();

        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterView),
                new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CartView),
            },
            "Test"));

        var list = await resolution.ListSelectableRegistersAsync(userId, principal);
        Assert.Single(list);
        Assert.Equal(openId, list[0].Id);
    }
}
