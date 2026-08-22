using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class CashRegisterResolutionServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegRes_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static CashRegisterResolutionService CreateService(AppDbContext ctx) =>
        new(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());

    private static ClaimsPrincipal PrincipalWithRole(string role)
    {
        var id = new ClaimsIdentity();
        id.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(id);
    }

    private static ClaimsPrincipal PrincipalWithAppPermissions(params string[] permissions)
    {
        var id = new ClaimsIdentity();
        foreach (var p in permissions)
            id.AddClaim(new Claim(PermissionCatalog.PermissionClaimType, p));
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsWhenSingleOperationalOpen_EvenWithDisabledRow()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "ARCH",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Equal(regId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsWhenSingleOperationalOpen_EvenWithMaintenanceRow()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "MNT",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Maintenance,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Equal(regId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsWhenSingleOperationalOpen_EvenWithInactiveClosedRow()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "OLD",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = false
        });
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Equal(regId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsWhenExactlyOneDbRow_AndThatRegisterIsOpen()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Equal(regId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_DoesNotAssignWhenSingleRegisterClosed()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Null(us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_DoesNotAssignWhenSoleRegisterOnAnotherUserShift()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Null(us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_DoesNotAssignWhenTwoRegisters()
    {
        await using var ctx = CreateContext();
        for (var i = 0; i < 2; i++)
        {
            ctx.CashRegisters.Add(new CashRegister
            {
                TenantId = SystemTenantIds.Platform,
                Id = Guid.NewGuid(),
                RegisterNumber = $"K{i}",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsDefaultForTenant = false
            });
        }

        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Null(us.CashRegisterId);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsTenantDefaultWhenMultipleOpen()
    {
        await using var ctx = CreateContext();
        var defaultId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = otherId,
            RegisterNumber = "K-OTHER",
            Location = "Bar",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDefaultForTenant = false
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = defaultId,
            RegisterNumber = "KASSE-001",
            Location = "Haupt",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDefaultForTenant = true
        });

        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Equal(defaultId.ToString(), us.CashRegisterId);
    }

    [Fact]
    public async Task ValidatePaymentRegister_SoleRegister_AllowsWithoutSettings()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    [Fact]
    public async Task ValidatePaymentRegister_StartbelegGateOn_WithoutStartbeleg_Blocks()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var sut = new CashRegisterResolutionService(
            ctx,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOnMissingStartbeleg(),
            RksvMonatsbelegTestDoubles.GateOnHasMonatsbeleg());

        var r = await sut.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());
        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.StartbelegRequired, r.Code);
    }

    [Fact]
    public async Task ValidatePaymentRegister_MonatsbelegGateOn_WithoutMonatsbeleg_Blocks()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var sut = new CashRegisterResolutionService(
            ctx,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOnHasStartbeleg(),
            RksvMonatsbelegTestDoubles.GateOnMissingMonatsbeleg());

        var r = await sut.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());
        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.MonatsbelegRequired, r.Code);
    }

    [Fact]
    public async Task ValidatePaymentRegister_MonatsbelegGateOn_WithPreviousMonthOnly_Allows()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var sut = new CashRegisterResolutionService(
            ctx,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOnHasStartbeleg(),
            RksvMonatsbelegTestDoubles.GateOnHasPreviousMonthOnly());

        var r = await sut.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());
        Assert.True(r.Ok);
    }

    [Fact]
    public async Task ValidatePaymentRegister_SingleOperationalOpen_AllowsWithoutSettings_WhenSecondRowIsDisabled()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "X",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    [Fact]
    public async Task ValidatePaymentRegister_SingleOperationalOpen_AllowsWithoutSettings_WhenSecondRowIsMaintenance()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "MNT",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Maintenance,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    [Fact]
    public async Task ValidatePaymentRegister_ConfiguredDefault_WorksWhenTwoOperationalRowsAndThirdIsDisabled()
    {
        await using var ctx = CreateContext();
        var rDefault = Guid.NewGuid();
        var rOther = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = rDefault,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = rOther,
            RegisterNumber = "K2",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "OLD",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = rDefault.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", rDefault, new ClaimsPrincipal());

        Assert.True(r.Ok);
        Assert.Equal(rDefault, r.ResolvedRegisterId);
    }

    [Fact]
    public async Task ValidatePaymentRegister_AssignmentMatchesButAnotherUserOwnsShift_ReturnsForbidden()
    {
        await using var ctx = CreateContext();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = r1,
            RegisterNumber = r1.ToString()[..8],
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = r2,
            RegisterNumber = r2.ToString()[..8],
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = r1.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", r1, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, r.Code);
    }

    [Fact]
    public async Task ValidatePaymentRegister_SoleRegister_OpenByOtherUser_ReturnsForbidden()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, r.Code);
    }

    [Fact]
    public async Task ListSelectableRegisters_WithoutCashRegisterView_ReturnsOnlyShiftOwnedOpenRegisters()
    {
        await using var ctx = CreateContext();
        var owned = Guid.NewGuid();
        var other = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = owned,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = other,
            RegisterNumber = "K2",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Single(list);
        Assert.Equal(owned, list[0].Id);
        Assert.Equal("K1", list[0].RegisterNumber);
    }

    /// <summary>
    /// Operational cardinality decides membership now: Open and Closed rows are listed, Maintenance / Disabled are not.
    /// </summary>
    [Fact]
    public async Task ListSelectableRegisters_ReturnsOpenAndClosed_ButNotMaintenanceOrDisabled()
    {
        await using var ctx = CreateContext();
        var openId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = openId,
            RegisterNumber = "K-OPEN",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "K-CLOSED",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "K-MNT",
            Location = "C",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Maintenance,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "K-DIS",
            Location = "D",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, r => r.Id == openId && r.RegisterNumber == "K-OPEN" && r.Status == RegisterStatus.Open);
        Assert.Contains(list, r => r.RegisterNumber == "K-CLOSED" && r.Status == RegisterStatus.Closed);
        Assert.DoesNotContain(list, r => r.RegisterNumber is "K-MNT" or "K-DIS");
    }

    [Fact]
    public async Task ListSelectableRegisters_WithCashRegisterView_ExcludesOpenRegistersHeldByOtherUsers()
    {
        await using var ctx = CreateContext();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = mine,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = theirs,
            RegisterNumber = "K2",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Single(list);
        Assert.Equal(mine, list[0].Id);
    }

    [Fact]
    public async Task ListSelectableRegisters_SoleDbRow_OpenByOtherUser_ReturnsEmpty()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Empty(list);
    }

    [Fact]
    public async Task ListSelectableForPosPicker_SoleRowOpenByOtherUser_ReturnsEmpty_WithNoneSelectableReason()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var result = await svc.ListSelectableForPosPickerAsync("u1", principal);

        Assert.Empty(result.Registers);
        Assert.Equal("none_selectable_for_user", result.EmptyReason);
    }

    [Fact]
    public async Task ListSelectableRegisters_TwoOpenRegistersUserOwnsNeither_ReturnsEmpty()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "K2",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u3",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Empty(list);
    }

    [Fact]
    public async Task ListSelectableRegisters_SoleOpenRegister_ReturnsOneRow_WithoutCashRegisterView()
    {
        await using var ctx = CreateContext();
        var onlyId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = onlyId,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Single(list);
        Assert.Equal(onlyId, list[0].Id);
    }

    [Fact]
    public async Task ListSelectableRegisters_SingleOperationalOpen_ReturnsOneRow_WithoutCashRegisterView_WhenSecondRowDisabled()
    {
        await using var ctx = CreateContext();
        var onlyId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = onlyId,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "X",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Single(list);
        Assert.Equal(onlyId, list[0].Id);
    }

    [Fact]
    public async Task ListSelectableForPosPicker_OnlyNonOperationalRows_EmptyReasonNoRegisters()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "D1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = Guid.NewGuid(),
            RegisterNumber = "M1",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Maintenance,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var result = await svc.ListSelectableForPosPickerAsync("u1", principal);

        Assert.Empty(result.Registers);
        Assert.Equal("no_registers", result.EmptyReason);
    }

    [Fact]
    public async Task ListSelectableRegisters_AllClosed_ReturnsClosedRowsWithStatus()
    {
        await using var ctx = CreateContext();
        var closedId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = closedId,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Single(list);
        Assert.Equal(closedId, list[0].Id);
        Assert.Equal(RegisterStatus.Closed, list[0].Status);
    }

    [Fact]
    public async Task ListSelectableForPosPicker_NoDbRows_EmptyReasonNoRegisters()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var result = await svc.ListSelectableForPosPickerAsync("u1", principal);
        Assert.Empty(result.Registers);
        Assert.Equal("no_registers", result.EmptyReason);
    }

    /// <summary>
    /// A closed register is a valid pick: POS opens it via shift auto-open on selection, so the picker must list it
    /// instead of reporting an empty list.
    /// </summary>
    [Fact]
    public async Task ListSelectableForPosPicker_AllClosed_ReturnsClosedRow_WithoutEmptyReason()
    {
        await using var db = CreateContext();
        var closedId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = closedId,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var result = await svc.ListSelectableForPosPickerAsync("u1", principal);

        Assert.Single(result.Registers);
        Assert.Equal(closedId, result.Registers[0].Id);
        Assert.Equal(RegisterStatus.Closed, result.Registers[0].Status);
        Assert.Null(result.EmptyReason);
    }

    [Fact]
    public async Task ListSelectableForPosPicker_OpenButNotSelectable_EmptyReasonNoneSelectableForUser()
    {
        await using var db = CreateContext();
        foreach (var uid in new[] { "other", "other2" })
        {
            db.CashRegisters.Add(new CashRegister
            {
                TenantId = SystemTenantIds.Platform,
                Id = Guid.NewGuid(),
                RegisterNumber = uid,
                Location = "A",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CurrentUserId = uid,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var result = await svc.ListSelectableForPosPickerAsync("u1", principal);
        Assert.Empty(result.Registers);
        Assert.Equal("none_selectable_for_user", result.EmptyReason);
    }

    [Fact]
    public async Task ValidatePaymentRegister_MultiRegister_RequiresAssignmentOrShift()
    {
        await using var ctx = CreateContext();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        foreach (var id in new[] { r1, r2 })
        {
            ctx.CashRegisters.Add(new CashRegister
            {
                TenantId = SystemTenantIds.Platform,
                Id = id,
                RegisterNumber = id.ToString()[..8],
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", r1, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.SelectionRequired, r.Code);
    }

    [Fact]
    public async Task ValidatePaymentRegister_ClosedRegister_Rejected()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Closed, r.Code);
    }

    /// <summary>
    /// Open → close lifecycle: after the register is closed, payment must fail with Closed (not a stale Open authorization).
    /// </summary>
    [Fact]
    public async Task ValidatePaymentRegister_OpenThenClosed_ReturnsClosed()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = regId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var reg = await ctx.CashRegisters.FirstAsync(r => r.Id == regId);
        reg.Status = RegisterStatus.Closed;
        reg.CurrentUserId = null;
        reg.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Closed, r.Code);
    }

    /// <summary>
    /// Shift owner may pay while open; after another user takes the shift, the original user must be forbidden (readiness conflict mirror).
    /// </summary>
    [Fact]
    public async Task ValidatePaymentRegister_ShiftOwner_Allows_ThenOtherUserShift_RejectsOriginalUser()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var first = await svc.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());
        Assert.True(first.Ok);

        var reg = await ctx.CashRegisters.FirstAsync(r => r.Id == regId);
        reg.CurrentUserId = "u2";
        await ctx.SaveChangesAsync();

        var svc2 = CreateService(ctx);
        var second = await svc2.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal());
        Assert.False(second.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, second.Code);
    }

    /// <summary>
    /// Assignment now turns on <see cref="CashRegister.AssignedUserId"/> rather than shift occupancy: an unassigned register
    /// may be stored as a preference even while another user holds the shift. Payment and the picker still block it.
    /// </summary>
    [Fact]
    public async Task ValidateAssignmentChange_UnassignedRegister_OpenByOtherUser_Succeeds()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), principal);

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    /// <summary>
    /// AppPermissions.CashRegisterView intentionally allows persisting assignment to an open register on another user&apos;s shift (sole DB row).
    /// Payment and POS picker still block until shift/ownership rules are satisfied — see companion tests.
    /// </summary>
    [Fact]
    public async Task ValidateAssignmentChange_SoleRegister_OpenByOtherUser_Succeeds_WithCashRegisterView()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), principal);

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    /// <summary>
    /// Same intentional assignment relaxation for multi-register: u1 may point settings at u2&apos;s open register when they have CashRegisterView.
    /// </summary>
    [Fact]
    public async Task ValidateAssignmentChange_MultiRegister_OpenByOtherUser_Succeeds_WithCashRegisterView()
    {
        await using var ctx = CreateContext();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = mine,
            RegisterNumber = "K1",
            Location = "A",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = theirs,
            RegisterNumber = "K2",
            Location = "B",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var r = await svc.ValidateAssignmentChangeAsync("u1", theirs.ToString(), principal);

        Assert.True(r.Ok);
        Assert.Equal(theirs, r.ResolvedRegisterId);
    }

    /// <summary>
    /// Occupancy is enforced before assignment match: CashRegisterView must not make payment succeed on another user&apos;s shift,
    /// even when UserSettings already reference that register (stale/matching assignment).
    /// </summary>
    [Fact]
    public async Task ValidatePaymentRegister_OtherUserShift_Forbidden_WithCashRegisterView_EvenWhenAssignmentMatches()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = regId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView, AppPermissions.PaymentTake);
        var r = await svc.ValidatePaymentRegisterAsync("u1", regId, principal);

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, r.Code);
    }

    /// <summary>
    /// POS picker never surfaces another user&apos;s occupied open register, even with CashRegisterView (assignment uses a separate API path).
    /// </summary>
    [Fact]
    public async Task ListSelectableRegisters_SoleOpenByOtherUser_Empty_WithCashRegisterView()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView);
        var list = await svc.ListSelectableRegistersAsync("u1", principal);

        Assert.Empty(list);
    }

    [Fact]
    public async Task ListSelectableRegisters_HidesRegistersAssignedToOtherUsers()
    {
        await using var ctx = CreateContext();
        var mine = Guid.NewGuid();
        var shared = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        AddRegister(ctx, mine, "K1", RegisterStatus.Closed, assignedUserId: "u1");
        AddRegister(ctx, shared, "K2", RegisterStatus.Open);
        AddRegister(ctx, theirs, "K3", RegisterStatus.Open, assignedUserId: "u2");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var list = await svc.ListSelectableRegistersAsync("u1", PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView));

        Assert.Equal(2, list.Count);
        Assert.Contains(list, r => r.Id == mine);
        Assert.Contains(list, r => r.Id == shared);
        Assert.DoesNotContain(list, r => r.Id == theirs);
    }

    /// <summary>Only Super Admin bypasses the assignment filter; <c>cash_register.view</c> no longer does (Cashier and Waiter both hold it).</summary>
    [Fact]
    public async Task ListSelectableRegisters_SuperAdmin_SeesRegistersAssignedToOtherUsers()
    {
        await using var ctx = CreateContext();
        var theirs = Guid.NewGuid();
        AddRegister(ctx, theirs, "K1", RegisterStatus.Open, assignedUserId: "u2");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var list = await svc.ListSelectableRegistersAsync("sa", PrincipalWithRole(Roles.SuperAdmin));

        Assert.Single(list);
        Assert.Equal(theirs, list[0].Id);
    }

    /// <summary>Assignment does not override occupancy: an assigned register on another user's shift stays out of the picker.</summary>
    [Fact]
    public async Task ListSelectableRegisters_AssignedToCallerButHeldByOtherShift_IsExcluded()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        AddRegister(ctx, regId, "K1", RegisterStatus.Open, assignedUserId: "u1", currentUserId: "u2");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var list = await svc.ListSelectableRegistersAsync("u1", PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView));

        Assert.Empty(list);
    }

    [Fact]
    public async Task ListSelectableForPosPicker_AllRegistersAssignedToOthers_EmptyReasonNoneAssigned()
    {
        await using var ctx = CreateContext();
        AddRegister(ctx, Guid.NewGuid(), "K1", RegisterStatus.Open, assignedUserId: "u2");
        AddRegister(ctx, Guid.NewGuid(), "K2", RegisterStatus.Closed, assignedUserId: "u3");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.ListSelectableForPosPickerAsync("u1", PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView));

        Assert.Empty(result.Registers);
        Assert.Equal("none_assigned", result.EmptyReason);
    }

    [Fact]
    public async Task ValidateAssignmentChange_ClosedRegister_Succeeds()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        AddRegister(ctx, regId, "K1", RegisterStatus.Closed);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), PrincipalWithAppPermissions(AppPermissions.CartView));

        Assert.True(r.Ok);
        Assert.Equal(regId, r.ResolvedRegisterId);
    }

    [Fact]
    public async Task ValidateAssignmentChange_RegisterAssignedToOtherUser_ReturnsForbidden()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        AddRegister(ctx, regId, "K1", RegisterStatus.Open, assignedUserId: "u2");
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), PrincipalWithAppPermissions(AppPermissions.CartView, AppPermissions.CashRegisterView));

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, r.Code);
    }

    [Theory]
    [InlineData(RegisterStatus.Maintenance)]
    [InlineData(RegisterStatus.Disabled)]
    public async Task ValidateAssignmentChange_NonOperationalStatus_ReturnsClosed(RegisterStatus status)
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        AddRegister(ctx, regId, "K1", status);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), PrincipalWithAppPermissions(AppPermissions.CartView));

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Closed, r.Code);
    }

    [Fact]
    public async Task ValidateAssignmentChange_DecommissionedRegister_ReturnsDecommissioned()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        AddRegister(ctx, regId, "K1", RegisterStatus.Decommissioned);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", regId.ToString(), PrincipalWithAppPermissions(AppPermissions.CartView));

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Decommissioned, r.Code);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_SkipsRegisterAssignedToAnotherUser()
    {
        await using var ctx = CreateContext();
        AddRegister(ctx, Guid.NewGuid(), "K1", RegisterStatus.Open, assignedUserId: "u2");
        var us = new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.UserSettings.Add(us);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        await svc.ApplySoleOpenRegisterAutoAssignmentIfNeededAsync(us, "u1");

        Assert.Null(us.CashRegisterId);
    }

    private static void AddRegister(
        AppDbContext ctx,
        Guid id,
        string registerNumber,
        RegisterStatus status,
        string? assignedUserId = null,
        string? currentUserId = null) =>
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = id,
            RegisterNumber = registerNumber,
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = status,
            AssignedUserId = assignedUserId,
            CurrentUserId = currentUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

    [Fact]
    public async Task ValidateAssignmentChange_InvalidGuid_ReturnsInvalid()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", "not-a-guid", PrincipalWithRole("Cashier"));
        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Invalid, r.Code);
    }

    [Fact]
    public async Task ValidateAssignmentChange_NonExistentRegister_ReturnsNotFound()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var r = await svc.ValidateAssignmentChangeAsync("u1", Guid.NewGuid().ToString(), PrincipalWithRole("Cashier"));
        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.NotFound, r.Code);
    }
}
