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

namespace KasseAPI_Final.Tests;

/// <summary>
/// POST create must leave the register Closed with no misleading Open row in cash_register_transactions.
/// </summary>
public class CashRegisterControllerCreateTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegCreate_{Guid.NewGuid()}")
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

    private static CashRegisterController CreateController(AppDbContext ctx, string userId)
    {
        var c = new CashRegisterController(
            Mock.Of<ILogger<CashRegisterController>>(),
            ctx,
            CreateTestUserManager(),
            Mock.Of<ICashRegisterShiftService>());
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterManage),
                    },
                    "Test")),
            },
        };
        return c;
    }

    [Fact]
    public async Task CreateCashRegister_LeavesRegisterClosed_WithNoOpenTransaction()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, "creator-1");

        var result = await controller.CreateCashRegister(new CreateCashRegisterModel
        {
            Location = "Store",
            StartingBalance = 123.45m,
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(created.Value);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync();
        Assert.Equal(RegisterStatus.Closed, reg.Status);
        Assert.Equal(123.45m, reg.CurrentBalance);
        Assert.Equal(123.45m, reg.StartingBalance);

        var txs = await ctx.CashRegisterTransactions.AsNoTracking().Where(t => t.CashRegisterId == reg.Id).ToListAsync();
        Assert.Empty(txs);
    }
}
