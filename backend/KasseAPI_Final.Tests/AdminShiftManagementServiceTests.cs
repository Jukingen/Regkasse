using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminShiftManagementServiceTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminShiftMgmt_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task ForceCloseRegisterAsync_ClosesOpenRegisterHeldByOtherUser()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        var regId = Guid.NewGuid();
        var ownerId = "owner-user";
        var actorId = "manager-user";

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 10m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = ownerId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var shiftSvc = new CashRegisterShiftService(
            ctx,
            Mock.Of<UserManager<ApplicationUser>>(),
            Mock.Of<ILogger<CashRegisterShiftService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var svc = new AdminShiftManagementService(
            ctx,
            shiftSvc,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<AdminShiftManagementService>>());

        var result = await svc.ForceCloseRegisterAsync(
            regId,
            actorId,
            Roles.Manager,
            closingBalance: 10m,
            reason: "test",
            CancellationToken.None);

        Assert.True(result.Success);
        var register = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Closed, register.Status);
        Assert.Null(register.CurrentUserId);
    }
}

public sealed class ShiftAutoCloseServiceTests
{
    [Fact]
    public async Task CloseStaleOpenRegistersAsync_ClosesRegisterOlderThanMaxHours()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ShiftAutoClose_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));

        var regId = Guid.NewGuid();
        var openedAt = DateTime.UtcNow.AddHours(-30);
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = openedAt,
            UpdatedAt = openedAt,
            Status = RegisterStatus.Open,
            CurrentUserId = "owner",
            IsActive = true,
            CreatedAt = openedAt,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "open",
            UserId = "owner",
            TransactionDate = openedAt,
            CreatedAt = openedAt,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var shiftSvc = new Mock<ICashRegisterShiftService>();
        shiftSvc
            .Setup(s => s.TryForceCloseCashRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, decimal, string, CancellationToken>((id, _, _, _, _) =>
            {
                var reg = ctx.CashRegisters.First(r => r.Id == id);
                reg.Status = RegisterStatus.Closed;
                reg.CurrentUserId = null;
                ctx.SaveChanges();
            })
            .ReturnsAsync(CashRegisterCloseResult.Success());

        var tenantAccessor = new MutableTenantAccessor { TenantId = tenantId };
        var svc = new ShiftAutoCloseService(
            ctx,
            shiftSvc.Object,
            tenantAccessor,
            Options.Create(new ShiftAutoCloseOptions { Enabled = true, MaxOpenDurationHours = 12 }),
            Mock.Of<ILogger<ShiftAutoCloseService>>());

        var closed = await svc.CloseStaleOpenRegistersAsync(CancellationToken.None);

        Assert.Equal(1, closed);
        var register = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Closed, register.Status);
    }

    [Fact]
    public async Task CloseStaleOpenRegistersAsync_SoftClosesOrphanActiveShift()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ShiftAutoCloseOrphan_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));

        var regId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-13);
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K2",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = startedAt,
            UpdatedAt = startedAt,
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = startedAt,
        });
        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            CashierId = "cashier-1",
            CashierName = "Max",
            StartBalance = 0,
            StartedAt = startedAt,
            Status = CashierShiftStatuses.Active,
            CreatedAt = startedAt,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var tenantAccessor = new MutableTenantAccessor { TenantId = tenantId };
        var svc = new ShiftAutoCloseService(
            ctx,
            Mock.Of<ICashRegisterShiftService>(),
            tenantAccessor,
            Options.Create(new ShiftAutoCloseOptions { Enabled = true, MaxOpenDurationHours = 12 }),
            Mock.Of<ILogger<ShiftAutoCloseService>>());

        var closedRegisters = await svc.CloseStaleOpenRegistersAsync(CancellationToken.None);

        Assert.Equal(0, closedRegisters);
        var shift = await ctx.CashierShifts.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(CashierShiftStatuses.Completed, shift.Status);
        Assert.True(shift.IsAutoClosed);
        Assert.Contains("inactivity", shift.Notes!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MutableTenantAccessor : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; }
    }
}
