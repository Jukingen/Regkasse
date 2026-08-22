using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminCashRegisters;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Legacy POST /api/CashRegister/{id}/open must resolve the actor from compact JWT claims
/// (<c>userId</c> / <c>sub</c>). JwtBearer uses MapInboundClaims=false, so
/// <see cref="ClaimTypes.NameIdentifier"/> is absent and SuperAdmin would otherwise get HTTP 401.
/// </summary>
public sealed class CashRegisterControllerOpenActorTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegOpenActor_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static UserManager<ApplicationUser> CreateTestUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static CashRegisterController CreateController(AppDbContext ctx, params Claim[] claims)
    {
        var shift = new CashRegisterShiftService(
            ctx,
            CreateTestUserManager(),
            Mock.Of<ILogger<CashRegisterShiftService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());
        var c = new CashRegisterController(
            Mock.Of<ILogger<CashRegisterController>>(),
            ctx,
            CreateTestUserManager(),
            shift,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform),
            Mock.Of<ICashRegisterManagementService>(),
            Mock.Of<ICashRegisterListEnrichmentService>(),
            LocalizationTestDoubles.ApiMessageLocalizer(),
            CashRegisterTestDoubles.PermissiveRegisterPermissions());
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            },
        };
        return c;
    }

    [Fact]
    public async Task Open_CompactJwtUserIdClaim_DoesNotReturnUnauthorized()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(
            ctx,
            new Claim("userId", "actor-1"),
            new Claim("sub", "actor-1"),
            new Claim("role", Roles.SuperAdmin),
            new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical));

        var result = await controller.OpenCashRegister(
            Guid.NewGuid(),
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsNotType<UnauthorizedObjectResult>(result);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Open_NameIdentifierOnly_WouldHaveFailedBeforeFix_NowStillWorksViaGetActorUserId()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(
            ctx,
            new Claim(ClaimTypes.NameIdentifier, "actor-1"));

        var result = await controller.OpenCashRegister(
            Guid.NewGuid(),
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsNotType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Open_WithoutActorClaims_ReturnsUnauthorized()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx);

        var result = await controller.OpenCashRegister(
            Guid.NewGuid(),
            new OpenCashRegisterModel { OpeningBalance = 0m },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
