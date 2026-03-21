using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
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
        return new AppDbContext(options);
    }

    private static CashRegisterResolutionService CreateService(AppDbContext ctx) =>
        new(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

    private static ClaimsPrincipal PrincipalWithRole(string role)
    {
        var id = new ClaimsIdentity();
        id.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_AssignsWhenExactlyOneDbRow_AndThatRegisterIsOpen()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
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
    public async Task ApplySoleOpenRegisterAutoAssignmentIfNeeded_DoesNotAssignWhenTwoRegisters()
    {
        await using var ctx = CreateContext();
        for (var i = 0; i < 2; i++)
        {
            ctx.CashRegisters.Add(new CashRegister
            {
                Id = Guid.NewGuid(),
                RegisterNumber = $"K{i}",
                Location = "L",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
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
    public async Task ValidatePaymentRegister_SoleRegister_AllowsWithoutSettings()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
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
    public async Task ValidatePaymentRegister_MultiRegister_RequiresAssignmentOrShift()
    {
        await using var ctx = CreateContext();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        foreach (var id in new[] { r1, r2 })
        {
            ctx.CashRegisters.Add(new CashRegister
            {
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
