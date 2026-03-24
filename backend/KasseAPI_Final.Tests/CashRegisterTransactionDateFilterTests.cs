using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Documents that <c>GET api/CashRegister/{{id}}/transactions</c> uses
/// <see cref="PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds"/> (same helper as the controller).
/// </summary>
public sealed class CashRegisterTransactionDateFilterTests
{
    [Fact]
    public void CashRegister_DateFilter_ParityWith_AustriaInclusiveCalendarRange_WhenBothBounds()
    {
        var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Unspecified);
        var (lo, hi) = PostgreSqlUtcDateTime.CalendarHalfOpenInstantBounds(start, end);
        var (expectedLo, expectedHi) = PostgreSqlUtcDateTime.AustriaInclusiveCalendarRangeUtc(start, end);
        Assert.Equal(expectedLo, lo);
        Assert.Equal(expectedHi, hi);
    }
}
