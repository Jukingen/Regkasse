using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

public class CashRegisterShiftServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ShiftSvc_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationUser user)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        return mgr;
    }

    [Fact]
    public async Task Open_IdempotentSecondCall_NoExtraOpenTransaction()
    {
        await using var ctx = CreateContext();
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
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        };

        var mgr = CreateUserManager(user);
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var first = await svc.TryOpenCashRegisterAsync(regId, "u1", 0m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.SuccessOpened, first.Kind);

        var openTxCount = await ctx.CashRegisterTransactions
            .CountAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Open);
        Assert.Equal(1, openTxCount);

        var second = await svc.TryOpenCashRegisterAsync(regId, "u1", 0m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.SuccessIdempotentAlreadyOpen, second.Kind);

        var openTxCountAfter = await ctx.CashRegisterTransactions
            .CountAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Open);
        Assert.Equal(1, openTxCountAfter);
    }

    [Fact]
    public async Task Open_ConflictWhenOpenByOtherUser()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        var other = new ApplicationUser
        {
            Id = "u2",
            UserName = "u2",
            Email = "u2@test",
            FirstName = "O",
            LastName = "T"
        };
        ctx.Users.Add(other);
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
            CurrentUserId = "u2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var user = new ApplicationUser { Id = "u1", UserName = "u1", Email = "u1@test", FirstName = "A", LastName = "B" };
        var mgr = CreateUserManager(user);
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryOpenCashRegisterAsync(regId, "u1", 0m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.FailedConflictOtherUser, result.Kind);
    }

    [Fact]
    public async Task Open_RejectedWhenActorAlreadyHasAnotherOpenRegister()
    {
        await using var ctx = CreateContext();
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        };
        ctx.Users.Add(user);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regA,
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
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regB,
            RegisterNumber = "K2",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var mgr = CreateUserManager(user);
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryOpenCashRegisterAsync(regB, "u1", 0m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister, result.Kind);

        var regBState = await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == regB);
        Assert.Equal(RegisterStatus.Closed, regBState.Status);
    }

    [Fact]
    public async Task Open_SetsCurrentUserId_OnSuccessfulOpen()
    {
        await using var ctx = CreateContext();
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
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        const string actorId = "actor-1";
        var user = new ApplicationUser
        {
            Id = actorId,
            UserName = actorId,
            Email = "a@test",
            FirstName = "A",
            LastName = "B"
        };
        var mgr = CreateUserManager(user);
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryOpenCashRegisterAsync(regId, actorId, 10m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.SuccessOpened, result.Kind);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Open, reg.Status);
        Assert.Equal(actorId, reg.CurrentUserId);
    }

    [Fact]
    public async Task Close_Owner_Succeeds_ClearsShift_AndWritesCloseTransaction()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string ownerId = "u1";
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
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var mgr = CreateUserManager(new ApplicationUser { Id = ownerId, UserName = ownerId, Email = "u@test" });
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryCloseCashRegisterAsync(regId, ownerId, 42m, CancellationToken.None);
        Assert.Equal(CashRegisterCloseKind.Success, result.Kind);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Closed, reg.Status);
        Assert.Null(reg.CurrentUserId);
        Assert.Equal(42m, reg.CurrentBalance);

        var closeTx = await ctx.CashRegisterTransactions
            .AsNoTracking()
            .SingleAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Close);
        Assert.Equal(42m, closeTx.Amount);
        Assert.Equal(ownerId, closeTx.UserId);
    }

    [Fact]
    public async Task Close_NonOwner_ReturnsForbidden_DoesNotMutate()
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
            CurrentBalance = 50,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = ownerId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var mgr = CreateUserManager(new ApplicationUser { Id = "other", UserName = "o", Email = "o@test" });
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryCloseCashRegisterAsync(regId, "other", 1m, CancellationToken.None);
        Assert.Equal(CashRegisterCloseKind.FailedForbidden, result.Kind);

        var reg = await ctx.CashRegisters.AsNoTracking().SingleAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Open, reg.Status);
        Assert.Equal(ownerId, reg.CurrentUserId);
        Assert.False(await ctx.CashRegisterTransactions.AnyAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Close));
    }

    [Fact]
    public async Task Close_AlreadyClosed_ReturnsAlreadyClosed()
    {
        await using var ctx = CreateContext();
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
            Status = RegisterStatus.Closed,
            CurrentUserId = null,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var mgr = CreateUserManager(new ApplicationUser { Id = "u1", UserName = "u1", Email = "u@test" });
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);

        var result = await svc.TryCloseCashRegisterAsync(regId, "u1", 0m, CancellationToken.None);
        Assert.Equal(CashRegisterCloseKind.FailedAlreadyClosed, result.Kind);
    }
}
