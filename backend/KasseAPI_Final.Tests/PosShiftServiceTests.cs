using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosShiftServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosShift_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationUser user)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        return mgr;
    }

    private static PosShiftService CreateService(
        AppDbContext ctx,
        ApplicationUser? actor = null,
        ICashRegisterShiftService? shift = null)
    {
        actor ??= new ApplicationUser
        {
            Id = "cashier-1",
            UserName = "cashier-1",
            Email = "cashier@test",
            FirstName = "Test",
            LastName = "Cashier",
        };

        var mgr = CreateUserManager(actor);
        var shiftSvc = shift ?? new CashRegisterShiftService(
            ctx,
            mgr.Object,
            Mock.Of<ILogger<CashRegisterShiftService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        return new PosShiftService(
            ctx,
            shiftSvc,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            mgr.Object,
            Mock.Of<ILogger<PosShiftService>>());
    }

    [Fact]
    public async Task GetCurrentShift_NoActive_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var result = await svc.GetCurrentShiftAsync("cashier-1");

        Assert.False(result.HasActiveShift);
        Assert.Null(result.Shift);
    }

    [Fact]
    public async Task StartShift_CreatesRow_AndOpensRegister()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var actor = new ApplicationUser
        {
            Id = userId,
            UserName = "k1",
            Email = "k1@test",
            FirstName = "Max",
            LastName = "Muster",
        };
        ctx.Users.Add(actor);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, actor);
        var dto = await svc.StartShiftAsync(userId, "fallback", new StartShiftRequest
        {
            CashRegisterId = regId,
            StartBalance = 100m,
        });

        Assert.Equal(CashierShiftStatuses.Active, dto.Status);
        Assert.Equal(100m, dto.StartBalance);
        Assert.Equal(regId, dto.CashRegisterId);

        var register = await ctx.CashRegisters.FindAsync(regId);
        Assert.Equal(RegisterStatus.Open, register!.Status);
        Assert.Equal(userId, register.CurrentUserId);
    }

    [Fact]
    public async Task StartShift_WhenAlreadyActive_Throws()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 50m,
            StartedAt = DateTime.UtcNow,
            Status = CashierShiftStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var ex = await Assert.ThrowsAsync<PosShiftStartException>(() =>
            svc.StartShiftAsync(userId, "Max", new StartShiftRequest { CashRegisterId = regId, StartBalance = 0 }));

        Assert.Equal(PosShiftStartResultKind.AlreadyActive, ex.Kind);
    }

    [Fact]
    public async Task EndShift_ComputesTotals_AndClosesRegister()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-2);

        var actor = new ApplicationUser { Id = userId, UserName = "k1", Email = "k1@test", FirstName = "Max", LastName = "M" };
        ctx.Users.Add(actor);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K2",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 100m,
            StartedAt = startedAt,
            Status = CashierShiftStatuses.Active,
            CreatedAt = startedAt,
            IsActive = true,
        });
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            TotalAmount = 30m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            CreatedAt = startedAt.AddMinutes(10),
            IsActive = true,
            ReceiptNumber = "R-1",
        });
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            TotalAmount = 20m,
            PaymentMethodRaw = ((int)PaymentMethod.Card).ToString(),
            CreatedAt = startedAt.AddMinutes(20),
            IsActive = true,
            ReceiptNumber = "R-2",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, actor);
        var endBalance = 130m; // 100 start + 30 cash sales
        var result = await svc.EndShiftAsync(userId, Roles.Cashier, new EndShiftRequest { EndBalance = endBalance });

        Assert.Equal(50m, result.Shift.TotalSales);
        Assert.Equal(30m, result.Shift.TotalCash);
        Assert.Equal(20m, result.Shift.TotalCard);
        Assert.Equal(0m, result.Shift.Difference);
        Assert.Equal(CashierShiftStatuses.Completed, result.Shift.Status);
        Assert.NotNull(result.Receipt.RegisterNumber);
        Assert.Equal("K2", result.Receipt.RegisterNumber);

        var register = await ctx.CashRegisters.FindAsync(regId);
        Assert.Equal(RegisterStatus.Closed, register!.Status);
        Assert.Null(register.CurrentUserId);
    }

    [Fact]
    public async Task GetCurrentShift_ReturnsLiveTotalsFromPayments()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-1);

        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 100m,
            StartedAt = startedAt,
            Status = CashierShiftStatuses.Active,
            CreatedAt = startedAt,
            IsActive = true,
        });
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            TotalAmount = 15m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            CreatedAt = startedAt.AddMinutes(5),
            IsActive = true,
            ReceiptNumber = "R-live",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.GetCurrentShiftAsync(userId);

        Assert.True(result.HasActiveShift);
        Assert.NotNull(result.Shift);
        Assert.Equal(15m, result.Shift!.TotalSales);
        Assert.Equal(15m, result.Shift.TotalCash);
        Assert.Equal(0m, result.Shift.TotalCard);
    }

    [Fact]
    public async Task AutoOpenShift_CreatesRow_WithIsAutoOpened()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var actor = new ApplicationUser
        {
            Id = userId,
            UserName = "k1",
            Email = "k1@test",
            FirstName = "Max",
            LastName = "Muster",
        };
        ctx.Users.Add(actor);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 25m,
            CurrentBalance = 25m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, actor);
        var dto = await svc.AutoOpenShiftAsync(userId, "fallback", regId);

        Assert.Equal(CashierShiftStatuses.Active, dto.Status);
        Assert.True(dto.IsAutoOpened);
        Assert.False(dto.IsAutoClosed);
        Assert.Equal(25m, dto.StartBalance);

        var register = await ctx.CashRegisters.FindAsync(regId);
        Assert.Equal(RegisterStatus.Open, register!.Status);
        Assert.Equal(userId, register.CurrentUserId);
    }

    [Fact]
    public async Task AutoOpenShift_WhenAlreadyActive_ReturnsExistingIdempotent()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        ctx.CashierShifts.Add(new CashierShift
        {
            Id = existingId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 50m,
            StartedAt = DateTime.UtcNow,
            Status = CashierShiftStatuses.Active,
            IsAutoOpened = true,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var dto = await svc.AutoOpenShiftAsync(userId, "Max", Guid.NewGuid());

        Assert.Equal(existingId, dto.Id);
        Assert.True(dto.IsAutoOpened);
        Assert.Equal(1, await ctx.CashierShifts.CountAsync());
    }

    [Fact]
    public async Task AutoCloseShift_CompletesWithoutClosingRegister()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-1);

        var actor = new ApplicationUser { Id = userId, UserName = "k1", Email = "k1@test", FirstName = "Max", LastName = "M" };
        ctx.Users.Add(actor);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K2",
            Location = "Front",
            StartingBalance = 100m,
            CurrentBalance = 130m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 100m,
            StartedAt = startedAt,
            Status = CashierShiftStatuses.Active,
            IsAutoOpened = true,
            CreatedAt = startedAt,
            IsActive = true,
        });
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            TotalAmount = 30m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            CreatedAt = startedAt.AddMinutes(10),
            IsActive = true,
            ReceiptNumber = "R-auto",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx, actor);
        var dto = await svc.AutoCloseShiftAsync(userId, Roles.Cashier);

        Assert.NotNull(dto);
        Assert.Equal(CashierShiftStatuses.Completed, dto!.Status);
        Assert.True(dto.IsAutoClosed);
        Assert.Equal(30m, dto.TotalSales);
        Assert.Equal(0m, dto.Difference);

        var register = await ctx.CashRegisters.FindAsync(regId);
        Assert.Equal(RegisterStatus.Open, register!.Status);
        Assert.Equal(userId, register.CurrentUserId);
    }

    [Fact]
    public async Task AutoCloseShift_WhenNoActive_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var dto = await svc.AutoCloseShiftAsync("cashier-1", Roles.Cashier);

        Assert.Null(dto);
    }
}
