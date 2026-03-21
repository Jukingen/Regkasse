using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>());

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
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>());

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
        var svc = new CashRegisterShiftService(ctx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>());

        var result = await svc.TryOpenCashRegisterAsync(regB, "u1", 0m, "open", allowIdempotentSameUser: true, CancellationToken.None);
        Assert.Equal(CashRegisterOpenKind.FailedActorAlreadyHasOtherOpenRegister, result.Kind);

        var regBState = await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == regB);
        Assert.Equal(RegisterStatus.Closed, regBState.Status);
    }
}
