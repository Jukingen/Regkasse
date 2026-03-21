using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosCashRegisterReadinessServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosReady_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static ClaimsPrincipal CashierPrincipal() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Cashier) }, "test"));

    private static ClaimsPrincipal WaiterPrincipal() =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, Roles.Waiter) }, "test"));

    private static PosCashRegisterReadinessService CreateSut(
        AppDbContext ctx,
        ICashRegisterShiftService shift,
        PosCashRegisterFeatureOptions featureOptions)
    {
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());
        var opt = Options.Create(featureOptions);
        return new PosCashRegisterReadinessService(
            ctx,
            resolution,
            shift,
            opt,
            Mock.Of<ILogger<PosCashRegisterReadinessService>>());
    }

    [Fact]
    public async Task SoleClosed_AutoOpenFlag_OpensAndReady()
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
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        await ctx.SaveChangesAsync();

        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mgr = new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        store.Setup(s => s.FindByIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.Users.First(u => u.Id == "u1"));

        var shift = new CashRegisterShiftService(ctx, mgr, Mock.Of<ILogger<CashRegisterShiftService>>());
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = true,
            DefaultAutoOpenOpeningBalance = 0
        };
        var sut = CreateSut(ctx, shift, features);

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("ready", dto.NextAction);
        Assert.True(dto.AutoOpened);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterAutoOpened, dto.MessageCode);
        Assert.Equal(regId.ToString(), dto.EffectiveRegisterId);
        Assert.Equal(regId.ToString("D"), dto.PreferredRegisterId);

        var reg = await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Open, reg.Status);
        Assert.Equal("u1", reg.CurrentUserId);
    }

    [Fact]
    public async Task SoleClosed_WithMaintenanceRow_StillAutoOpens_WhenSingleOperational()
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
        ctx.CashRegisters.Add(new CashRegister
        {
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
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        await ctx.SaveChangesAsync();

        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mgr = new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        store.Setup(s => s.FindByIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.Users.First(u => u.Id == "u1"));

        var shift = new CashRegisterShiftService(ctx, mgr, Mock.Of<ILogger<CashRegisterShiftService>>());
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = true,
            DefaultAutoOpenOpeningBalance = 0
        };
        var sut = CreateSut(ctx, shift, features);

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("ready", dto.NextAction);
        Assert.True(dto.AutoOpened);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterAutoOpened, dto.MessageCode);
        Assert.Equal(regId.ToString(), dto.EffectiveRegisterId);
    }

    // Sole closed → ensure-ready auto-opens → payment authorization allows same register (server-side mirror of Zahlen prerequisites).
    [Fact]
    public async Task SoleClosed_EnsureReadyAutoOpen_ThenValidatePayment_AllowsSameRegister()
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
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        await ctx.SaveChangesAsync();

        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mgr = new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        store.Setup(s => s.FindByIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.Users.First(u => u.Id == "u1"));

        var shift = new CashRegisterShiftService(ctx, mgr, Mock.Of<ILogger<CashRegisterShiftService>>());
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = true,
            DefaultAutoOpenOpeningBalance = 0
        };
        var sut = CreateSut(ctx, shift, features);
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        Assert.Equal("ready", dto.NextAction);
        Assert.Equal(regId.ToString(), dto.EffectiveRegisterId);

        var pay = await resolution.ValidatePaymentRegisterAsync("u1", regId, CashierPrincipal());
        Assert.True(pay.Ok);
        Assert.Equal(regId, pay.ResolvedRegisterId);
    }

    [Fact]
    public async Task SoleOpen_SameUser_IdempotentReady_NoDuplicateOpenTx()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        ctx.CashRegisters.Add(new CashRegister
        {
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

        var shift = new Mock<ICashRegisterShiftService>();
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = true
        };
        var sut = CreateSut(ctx, shift.Object, features);

        var before = await ctx.CashRegisterTransactions.CountAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Open);
        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        var after = await ctx.CashRegisterTransactions.CountAsync(t => t.CashRegisterId == regId && t.TransactionType == TransactionType.Open);

        Assert.Equal("ready", dto.NextAction);
        Assert.False(dto.AutoOpened);
        Assert.Equal(before, after);
        shift.Verify(
            s => s.TryOpenCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SoleOpen_ByOtherUser_Conflict()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", Email = "a@test", FirstName = "A", LastName = "B" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2", Email = "b@test", FirstName = "C", LastName = "D" });
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

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true, AutoOpenSoleClosedRegister = true });

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("forbidden", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterConflict, dto.MessageCode);
        Assert.Null(dto.PreferredRegisterId);
        Assert.Equal(regId.ToString("D"), dto.EffectiveRegisterId);
    }

    /// <summary>
    /// Payment-time validation must reject the same sole-register shift conflict ensure-ready surfaces (POS client may still POST the register id).
    /// </summary>
    [Fact]
    public async Task SoleOpen_ByOtherUser_EnsureReadyConflict_ThenValidatePayment_Rejected()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", Email = "a@test", FirstName = "A", LastName = "B" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2", Email = "b@test", FirstName = "C", LastName = "D" });
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

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true, AutoOpenSoleClosedRegister = true });
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        Assert.Equal("forbidden", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterConflict, dto.MessageCode);

        var pay = await resolution.ValidatePaymentRegisterAsync("u1", regId, CashierPrincipal());
        Assert.False(pay.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, pay.Code);
    }

    /// <summary>
    /// Stale profile assignment to the effective register while another user holds the shift: ensure-ready forbids, payment must not succeed on that register id.
    /// </summary>
    [Fact]
    public async Task StaleSettings_AssignedSoleOpen_ByOtherUser_EnsureReadyConflict_ThenValidatePayment_Rejected()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", Email = "a@test", FirstName = "A", LastName = "B" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2", Email = "b@test", FirstName = "C", LastName = "D" });
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
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = regId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true, AutoOpenSoleClosedRegister = true });
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        Assert.Equal("forbidden", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterConflict, dto.MessageCode);
        Assert.Equal(regId.ToString("D"), dto.PreferredRegisterId);
        Assert.Equal(regId.ToString("D"), dto.EffectiveRegisterId);

        var pay = await resolution.ValidatePaymentRegisterAsync("u1", regId, CashierPrincipal());
        Assert.False(pay.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, pay.Code);
    }

    /// <summary>
    /// Multi-register: assigned effective register is open under another user — same conflict as readiness, payment POST must fail.
    /// </summary>
    [Fact]
    public async Task AssignedOpen_ByOtherUser_EnsureReadyConflict_ThenValidatePayment_Rejected()
    {
        await using var ctx = CreateContext();
        var rTaken = Guid.NewGuid();
        var rOther = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1", Email = "a@test", FirstName = "A", LastName = "B" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2", Email = "b@test", FirstName = "C", LastName = "D" });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = rTaken,
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
        ctx.CashRegisters.Add(new CashRegister
        {
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
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = rTaken.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true, AutoOpenSoleClosedRegister = false });
        var resolution = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);
        Assert.Equal("forbidden", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterConflict, dto.MessageCode);
        Assert.Equal(rTaken.ToString("D"), dto.PreferredRegisterId);

        var pay = await resolution.ValidatePaymentRegisterAsync("u1", rTaken, CashierPrincipal());
        Assert.False(pay.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, pay.Code);
    }

    [Fact]
    public async Task NoRegister_Required()
    {
        await using var ctx = CreateContext();
        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true });

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("none", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterRequired, dto.MessageCode);
    }

    [Fact]
    public async Task OnlyNonOperationalRows_ReadinessRequired_LikeEmptyTable()
    {
        await using var ctx = CreateContext();
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            RegisterNumber = "D1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Disabled,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions { EffectiveDefaultOnPosEntry = true });

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("none", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterRequired, dto.MessageCode);
    }

    [Fact]
    public async Task SoleClosed_FlagOff_OpenRegisterAction()
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

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = false
        });

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("open_register", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterClosed, dto.MessageCode);
        Assert.False(dto.AutoOpened);
        shift.Verify(
            s => s.TryOpenCashRegisterAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignedDefault_Closed_AutoOpensWhenFlag_MultiRegister()
    {
        await using var ctx = CreateContext();
        var reg1 = Guid.NewGuid();
        var reg2 = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = reg1,
            RegisterNumber = "K1",
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
            Id = reg2,
            RegisterNumber = "K2",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = reg2.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mgr = new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        store.Setup(s => s.FindByIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.Users.First(u => u.Id == "u1"));

        var shift = new CashRegisterShiftService(ctx, mgr, Mock.Of<ILogger<CashRegisterShiftService>>());
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = false,
            AutoOpenAssignedClosedRegister = true,
            DefaultAutoOpenOpeningBalance = 0
        };
        var sut = CreateSut(ctx, shift, features);

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("ready", dto.NextAction);
        Assert.True(dto.AutoOpened);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterAutoOpened, dto.MessageCode);
        Assert.Equal(reg2.ToString(), dto.EffectiveRegisterId);

        var opened = await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == reg2);
        Assert.Equal(RegisterStatus.Open, opened.Status);
        Assert.Equal("u1", opened.CurrentUserId);
    }

    [Fact]
    public async Task AutoOpen_AssignedClosed_BlockedWhenActorAlreadyHasOtherOpenRegister()
    {
        await using var ctx = CreateContext();
        var regOpen = Guid.NewGuid();
        var regAssignedClosed = Guid.NewGuid();
        ctx.Users.Add(new ApplicationUser
        {
            Id = "u1",
            UserName = "u1",
            Email = "u1@test",
            FirstName = "A",
            LastName = "B"
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regOpen,
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
            Id = regAssignedClosed,
            RegisterNumber = "K2",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = "u1",
            CashRegisterId = regAssignedClosed.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var store = new Mock<Microsoft.AspNetCore.Identity.IUserStore<ApplicationUser>>();
        var mgr = new Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        store.Setup(s => s.FindByIdAsync("u1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx.Users.First(u => u.Id == "u1"));

        var shift = new CashRegisterShiftService(ctx, mgr, Mock.Of<ILogger<CashRegisterShiftService>>());
        var features = new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = false,
            AutoOpenAssignedClosedRegister = true,
            DefaultAutoOpenOpeningBalance = 0
        };
        var sut = CreateSut(ctx, shift, features);

        var dto = await sut.EnsureReadyForPosAsync("u1", CashierPrincipal(), CancellationToken.None);

        Assert.Equal("forbidden", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterActorAlreadyOpenElsewhere, dto.MessageCode);

        var stillClosed = await ctx.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == regAssignedClosed);
        Assert.Equal(RegisterStatus.Closed, stillClosed.Status);
    }

    [Fact]
    public async Task SoleClosed_NoShiftOpen_Forbidden()
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

        var shift = new Mock<ICashRegisterShiftService>();
        var sut = CreateSut(ctx, shift.Object, new PosCashRegisterFeatureOptions
        {
            EffectiveDefaultOnPosEntry = true,
            AutoOpenSoleClosedRegister = true
        });

        var dto = await sut.EnsureReadyForPosAsync("u1", WaiterPrincipal(), CancellationToken.None);

        Assert.Equal("open_register", dto.NextAction);
        Assert.Equal(PosCashRegisterReadinessMessageCodes.CashRegisterForbidden, dto.MessageCode);
    }
}
