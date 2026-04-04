using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Close cash register: authorization uses <see cref="CashRegister.CurrentUserId"/> scalar only (no navigation load);
/// success clears <see cref="CashRegister.CurrentUserId"/> and <see cref="CashRegister.CurrentUser"/> and writes close transaction.
/// </summary>
public class CashRegisterControllerCloseTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegClose_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateTestUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static CashRegisterController CreateController(AppDbContext ctx, string authenticatedUserId)
    {
        var shift = new CashRegisterShiftService(
            ctx,
            CreateTestUserManager(),
            Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var c = new CashRegisterController(
            Mock.Of<ILogger<CashRegisterController>>(),
            ctx,
            CreateTestUserManager(),
            shift,
            TenantTestDoubles.PrimaryTenantResolver);
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, authenticatedUserId),
                        new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.ShiftClose),
                    },
                    "Test")),
            },
        };
        return c;
    }

    [Fact]
    public async Task CloseCashRegister_Owner_Succeeds_AndClearsOwnership_AndAddsCloseTransaction()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string ownerId = "owner-1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = ownerId,
            UserName = "owner",
            Email = "o@test",
            FirstName = "O",
            LastName = "W",
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 50,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, ownerId);
        var body = new CloseCashRegisterModel { ClosingBalance = 42m };
        var actionResult = await controller.CloseCashRegister(regId, body, CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Closed, reg.Status);
        Assert.Null(reg.CurrentUserId);
        Assert.Equal(42m, reg.CurrentBalance);

        var closeTx = await ctx.CashRegisterTransactions
            .AsNoTracking()
            .SingleAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Close);
        Assert.Equal(42m, closeTx.Amount);
        Assert.Equal(ownerId, closeTx.UserId);
        Assert.NotEqual(default, closeTx.TransactionDate);
    }

    [Fact]
    public async Task CloseCashRegister_NonOwner_ReturnsForbid_DoesNotMutateRegister()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string ownerId = "owner-1";
        const string otherId = "other-2";
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 50,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, otherId);
        var actionResult = await controller.CloseCashRegister(regId, new CloseCashRegisterModel { ClosingBalance = 1m }, CancellationToken.None);

        Assert.IsType<ForbidResult>(actionResult);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Open, reg.Status);
        Assert.Equal(ownerId, reg.CurrentUserId);
        Assert.Equal(50m, reg.CurrentBalance);
        Assert.False(await ctx.CashRegisterTransactions.AnyAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Close));
    }

    [Fact]
    public async Task CloseCashRegister_AlreadyClosed_ReturnsBadRequest()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string ownerId = "owner-1";
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CurrentUserId = null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, ownerId);
        var actionResult = await controller.CloseCashRegister(regId, new CloseCashRegisterModel { ClosingBalance = 0m }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }
}
