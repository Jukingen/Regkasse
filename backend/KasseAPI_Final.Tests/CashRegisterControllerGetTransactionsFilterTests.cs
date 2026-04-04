using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
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
/// Endpoint-level check: <see cref="CashRegisterController.GetCashRegisterTransactions"/> applies the same half-open bounds as <see cref="PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds"/>.
/// </summary>
public sealed class CashRegisterControllerGetTransactionsFilterTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegGetTx_{Guid.NewGuid()}")
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
                        new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.CashRegisterView),
                    },
                    "Test")),
            },
        };
        return c;
    }

    /// <summary>Controller now requires a tenant-scoped cash register row before listing transactions.</summary>
    private static void SeedTenantCashRegister(AppDbContext ctx, Guid regId)
    {
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "TX-SEED",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
    }

    private static List<CashRegisterTransaction> GetTransactionsFromOk(OkObjectResult ok)
    {
        var prop = ok.Value!.GetType().GetProperty("transactions", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        Assert.NotNull(prop);
        var raw = prop.GetValue(ok.Value);
        var enumerable = Assert.IsAssignableFrom<IEnumerable<CashRegisterTransaction>>(raw);
        return enumerable.ToList();
    }

    [Fact]
    public async Task GetCashRegisterTransactions_EndOnly_ReturnsRowsInsideViennaDay()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "viewer-1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "v",
            Email = "v@test",
            FirstName = "V",
            LastName = "W",
        });
        SeedTenantCashRegister(ctx, regId);
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 7, 4);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(day, day);
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "in",
            TransactionDate = lo.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "out",
            TransactionDate = hi,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, uid);
        var end = new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Unspecified);
        var result = await controller.GetCashRegisterTransactions(regId, null, end);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = GetTransactionsFromOk(ok);
        Assert.Single(list);
        Assert.Equal("in", list[0].Description);
    }

    /// <summary>
    /// Endpoint path: start-only — same selection as in-memory LINQ helper tests (lower inclusive, no upper).
    /// </summary>
    [Fact]
    public async Task GetCashRegisterTransactions_StartOnly_IncludesFromViennaDay_Onward()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "viewer-2";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "v2",
            Email = "v2@test",
            FirstName = "V",
            LastName = "2",
        });
        SeedTenantCashRegister(ctx, regId);
        var day10 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 11, 3);
        var (lo10, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day10);
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "before",
            TransactionDate = lo10.AddTicks(-1),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "on",
            TransactionDate = lo10.AddHours(1),
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, uid);
        var start = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Unspecified);
        var result = await controller.GetCashRegisterTransactions(regId, start, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = GetTransactionsFromOk(ok);
        Assert.Single(list);
        Assert.Equal("on", list[0].Description);
    }

    /// <summary>
    /// Endpoint path: both calendar bounds — multi-day Vienna range, rows strictly inside half-open UTC interval.
    /// </summary>
    [Fact]
    public async Task GetCashRegisterTransactions_BothBounds_ExcludesOutsideInterval()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "viewer-3";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "v3",
            Email = "v3@test",
            FirstName = "V",
            LastName = "3",
        });
        SeedTenantCashRegister(ctx, regId);
        var start = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 4, 3, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(start, end);
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "before",
            TransactionDate = lo.AddTicks(-1),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "in",
            TransactionDate = lo,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "after",
            TransactionDate = hi,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, uid);
        var result = await controller.GetCashRegisterTransactions(regId, start, end);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = GetTransactionsFromOk(ok);
        Assert.Single(list);
        Assert.Equal("in", list[0].Description);
    }

    /// <summary>
    /// Endpoint path: no date query params — no date filter on transactions (all rows for register).
    /// </summary>
    [Fact]
    public async Task GetCashRegisterTransactions_NullBounds_ReturnsAllTransactionsForRegister()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "viewer-4";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "v4",
            Email = "v4@test",
            FirstName = "V",
            LastName = "4",
        });
        SeedTenantCashRegister(ctx, regId);
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "x",
            TransactionDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "y",
            TransactionDate = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, uid);
        var result = await controller.GetCashRegisterTransactions(regId, null, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = GetTransactionsFromOk(ok);
        Assert.Equal(2, list.Count);
    }

    /// <summary>
    /// Endpoint path: single Vienna calendar day on DST spring — real controller query matches half-open day length.
    /// </summary>
    [Fact]
    public async Task GetCashRegisterTransactions_EndOnly_DstSpring_IncludesOnlyRowsInsideViennaDay()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "viewer-5";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "v5",
            Email = "v5@test",
            FirstName = "V",
            LastName = "5",
        });
        SeedTenantCashRegister(ctx, regId);
        var d = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "inside",
            TransactionDate = lo.AddMinutes(90),
            CreatedAt = DateTime.UtcNow,
        });
        ctx.CashRegisterTransactions.Add(new CashRegisterTransaction
        {
            CashRegisterId = regId,
            UserId = uid,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = "nextUtcDay",
            TransactionDate = hi,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx, uid);
        var result = await controller.GetCashRegisterTransactions(regId, null, d);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = GetTransactionsFromOk(ok);
        Assert.Single(list);
        Assert.Equal("inside", list[0].Description);
    }
}
