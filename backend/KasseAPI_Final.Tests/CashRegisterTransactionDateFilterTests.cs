using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Mirrors <c>GET api/CashRegister/{{id}}/transactions</c> date filtering (same LINQ as <see cref="CashRegisterController.GetCashRegisterTransactions"/>).
/// </summary>
public sealed class CashRegisterTransactionDateFilterTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CashRegTxFilter_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CashRegisterTransaction Tx(
        Guid cashRegisterId,
        string userId,
        DateTime transactionDateUtc,
        string description)
    {
        return new CashRegisterTransaction
        {
            CashRegisterId = cashRegisterId,
            UserId = userId,
            TransactionType = TransactionType.Open,
            Amount = 0,
            Description = description,
            TransactionDate = transactionDateUtc,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private static async Task<List<CashRegisterTransaction>> QueryLikeControllerAsync(
        AppDbContext ctx,
        Guid cashRegisterId,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = ctx.CashRegisterTransactions.Where(t => t.CashRegisterId == cashRegisterId);
        var (lowerInclusiveUtc, upperExclusiveUtc) =
            PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(startDate, endDate);
        if (lowerInclusiveUtc.HasValue)
            query = query.Where(t => t.TransactionDate >= lowerInclusiveUtc.Value);
        if (upperExclusiveUtc.HasValue)
            query = query.Where(t => t.TransactionDate < upperExclusiveUtc.Value);
        return await query.OrderBy(t => t.TransactionDate).AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Query_StartOnly_IncludesFromViennaCalendarDay_Onward()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var day10 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 6, 10);
        var (lo10, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day10);
        var before = lo10.AddTicks(-1);
        var onDay = lo10.AddHours(2);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, before, "before"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, onDay, "on"));
        await ctx.SaveChangesAsync();

        var start = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var rows = await QueryLikeControllerAsync(ctx, regId, start, null);

        Assert.Single(rows);
        Assert.Equal("on", rows[0].Description);
    }

    [Fact]
    public async Task Query_EndOnly_SelectsSingleViennaCalendarDay_HalfOpen()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 6, 10);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(day, day);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo, "start"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi.AddTicks(-1), "lastIncluded"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "exclusiveEnd"));
        await ctx.SaveChangesAsync();

        var end = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var rows = await QueryLikeControllerAsync(ctx, regId, null, end);

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => r.Description == "exclusiveEnd");
    }

    [Fact]
    public async Task Query_BothBounds_MultiDay_IncludesOnlyRange()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var start = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(start, end);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo.AddTicks(-1), "before"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo, "in"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi.AddTicks(-1), "last"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "after"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, start, end);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Description == "in");
        Assert.Contains(rows, r => r.Description == "last");
    }

    [Fact]
    public async Task Query_NoBounds_ReturnsAllForRegister()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, DateTime.UtcNow, "a"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, DateTime.UtcNow.AddDays(-1), "b"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, null, null);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task Query_DstSpring2026_March29_IncludesTransactionInsideViennaDay()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var d = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo.AddHours(2), "inside"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "nextDay"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, d, d);

        Assert.Single(rows);
        Assert.Equal("inside", rows[0].Description);
    }

    [Fact]
    public async Task Query_DstFall2026_October25_IncludesTransactionInsideViennaDay()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var d = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo.AddHours(3), "inside"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "nextDay"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, d, d);

        Assert.Single(rows);
        Assert.Equal("inside", rows[0].Description);
    }

    /// <summary>
    /// Lower bound is inclusive: <c>TransactionDate &gt;= lo</c>; one tick before <c>lo</c> is excluded (end-only single Vienna day).
    /// </summary>
    [Fact]
    public async Task Query_EndOnly_ExcludesOneTickBeforeViennaDayStart_IncludesExactLowerUtc()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 8, 15);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(day, day);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo.AddTicks(-1), "tickBeforeLo"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo, "atLoInclusive"));
        await ctx.SaveChangesAsync();

        var end = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var rows = await QueryLikeControllerAsync(ctx, regId, null, end);

        Assert.Single(rows);
        Assert.Equal("atLoInclusive", rows[0].Description);
    }

    /// <summary>
    /// Vienna calendar day can start on a UTC calendar <em>previous</em> date (CEST). Filter uses Vienna semantics, not <see cref="DateTime.Date"/> in UTC.
    /// </summary>
    [Fact]
    public async Task Query_EndOnly_IncludesViennaDayStartEvenWhenUtcCalendarDateDiffers()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 6, 10);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(day, day);
        Assert.NotEqual(lo.Day, day.Day);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo, "viennaJune10StartUtc"));
        await ctx.SaveChangesAsync();

        var end = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Unspecified);
        var rows = await QueryLikeControllerAsync(ctx, regId, null, end);

        Assert.Single(rows);
        Assert.Equal("viennaJune10StartUtc", rows[0].Description);
    }

    /// <summary>
    /// <c>startDate</c> and <c>endDate</c> same calendar day → same half-open range as end-only for that day.
    /// </summary>
    [Fact]
    public async Task Query_SameDay_StartAndEndEqual_IncludesOnlyThatViennaDay()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var d = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, lo.AddHours(1), "mid"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "after"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, d, d);

        Assert.Single(rows);
        Assert.Equal("mid", rows[0].Description);
    }

    /// <summary>
    /// Upper bound is exclusive: <c>TransactionDate &lt; hi</c>; last tick before <c>hi</c> included, <c>hi</c> excluded (DST spring day).
    /// </summary>
    [Fact]
    public async Task Query_DstSpring_Boundary_LastTickBeforeUpperExclusive_Included_UpperExcluded()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var d = new DateTime(2026, 3, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi.AddTicks(-1), "lastTickIncluded"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "firstInstantExcluded"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, d, d);

        Assert.Single(rows);
        Assert.Equal("lastTickIncluded", rows[0].Description);
    }

    /// <summary>
    /// Fall-back day: same boundary semantics on a 25h Vienna day.
    /// </summary>
    [Fact]
    public async Task Query_DstFall_Boundary_LastTickBeforeUpperExclusive_Included()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        const string uid = "u1";
        ctx.Users.Add(new ApplicationUser
        {
            Id = uid,
            UserName = "u",
            Email = "u@test",
            FirstName = "U",
            LastName = "1",
        });
        var d = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(d, d);
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi.AddTicks(-1), "lastTickIncluded"));
        ctx.CashRegisterTransactions.Add(Tx(regId, uid, hi, "excluded"));
        await ctx.SaveChangesAsync();

        var rows = await QueryLikeControllerAsync(ctx, regId, d, d);

        Assert.Single(rows);
        Assert.Equal("lastTickIncluded", rows[0].Description);
    }
}
