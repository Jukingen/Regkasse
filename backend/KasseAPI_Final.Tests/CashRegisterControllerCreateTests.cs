using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// POST create must leave the register Closed with no misleading Open row in cash_register_transactions.
/// </summary>
public class CashRegisterControllerCreateTests
{
    private static readonly Guid PrimaryTenantId = LegacyDefaultTenantIds.Primary;
    private static readonly Guid OtherTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegCreate_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Tenants.AddRange(
            new Tenant { Id = PrimaryTenantId, Name = "Primary", Slug = "primary" },
            new Tenant { Id = OtherTenantId, Name = "Other", Slug = "other" });
        ctx.SaveChanges();
        return ctx;
    }

    private static UserManager<ApplicationUser> CreateTestUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static CashRegisterController CreateController(
        AppDbContext ctx,
        string userId,
        string? role = null,
        ISettingsTenantResolver? tenantResolver = null,
        bool includeManagePermission = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
        };
        if (includeManagePermission)
            claims.Add(new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterManage));
        if (!string.IsNullOrEmpty(role))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var c = new CashRegisterController(
            Mock.Of<ILogger<CashRegisterController>>(),
            ctx,
            CreateTestUserManager(),
            Mock.Of<ICashRegisterShiftService>(),
            tenantResolver ?? TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId),
            new CashRegisterManagementService(
                ctx,
                tenantResolver ?? TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId),
                Mock.Of<IAuditLogService>(),
                NullLogger<CashRegisterManagementService>.Instance));
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
    public async Task CreateCashRegister_LeavesRegisterClosed_WithNoOpenTransaction()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, "creator-1", Roles.Manager);

        var result = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-001",
            Location = "Store",
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(created.Value);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync();
        Assert.Equal(RegisterStatus.Closed, reg.Status);
        Assert.Equal("KASSE-001", reg.RegisterNumber);
        Assert.Equal(PrimaryTenantId, reg.TenantId);
        Assert.Equal(0m, reg.CurrentBalance);
        Assert.Equal(0m, reg.StartingBalance);

        var txs = await ctx.CashRegisterTransactions.AsNoTracking().Where(t => t.CashRegisterId == reg.Id).ToListAsync();
        Assert.Empty(txs);
    }

    [Fact]
    public async Task CreateCashRegister_Manager_CanCreateOnlyForOwnTenant()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(
            ctx,
            "manager-1",
            Roles.Manager,
            TenantTestDoubles.SettingsResolverReturning(PrimaryTenantId));

        var ownTenantResult = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
        }, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(ownTenantResult);
        var ownRegister = await ctx.CashRegisters.AsNoTracking().SingleAsync();
        Assert.Equal(PrimaryTenantId, ownRegister.TenantId);

        var crossTenantResult = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-002",
            Location = "Theke",
            TenantId = OtherTenantId,
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(crossTenantResult);
        Assert.Single(await ctx.CashRegisters.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreateCashRegister_SuperAdmin_CanCreateForAnyTenant()
    {
        await using var ctx = CreateContext();
        var controller = CreateController(ctx, "super-1", Roles.SuperAdmin);

        var otherTenantResult = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-001",
            Location = "Hauptkasse",
            TenantId = OtherTenantId,
        }, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(otherTenantResult);

        var primaryTenantResult = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-002",
            Location = "Theke",
        }, CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(primaryTenantResult);

        var registers = await ctx.CashRegisters.AsNoTracking().OrderBy(r => r.RegisterNumber).ToListAsync();
        Assert.Equal(2, registers.Count);
        Assert.Equal(OtherTenantId, registers[0].TenantId);
        Assert.Equal(PrimaryTenantId, registers[1].TenantId);
    }

    [Fact]
    public async Task CreateCashRegister_DuplicateRegisterNumber_ReturnsConflict()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = PrimaryTenantId,
            RegisterNumber = "KASSE-001",
            Location = "Existing",
            StartingBalance = 0m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, "creator-1", Roles.Manager);
        var result = await controller.CreateCashRegister(new CreateCashRegisterRequest
        {
            RegisterNumber = "KASSE-001",
            Location = "New",
        }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
