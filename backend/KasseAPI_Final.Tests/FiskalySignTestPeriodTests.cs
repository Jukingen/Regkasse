using KasseAPI_Final.DTOs;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse.Fiskaly;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalySignTestPeriodTests
{
    [Fact]
    public void TryResolve_UsesExplicitPastYearAndMonth()
    {
        var ok = FiskalySignTestPeriod.TryResolve(
            new FiskalySignTestRequest { Year = 2025, Month = 3 },
            yearly: false,
            out var year,
            out var month,
            out var error);

        Assert.True(ok);
        Assert.Equal(2025, year);
        Assert.Equal(3, month);
        Assert.Null(error);
    }

    [Fact]
    public void TryResolve_InvalidMonth_Fails()
    {
        var ok = FiskalySignTestPeriod.TryResolve(
            new FiskalySignTestRequest { Year = 2026, Month = 13 },
            yearly: false,
            out _,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("Month", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolve_ClosingDateUtc_UsesPreviousCompletedViennaMonth()
    {
        var ok = FiskalySignTestPeriod.TryResolve(
            new FiskalySignTestRequest
            {
                ClosingDate = new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc)
            },
            yearly: false,
            out var year,
            out var month,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(2025, year);
        Assert.Equal(12, month);
    }

    [Fact]
    public void TryResolve_OmittedMonthly_UsesViennaPreviousMonth()
    {
        var (expectedYear, expectedMonth) = PostgreSqlUtcDateTime.GetViennaPreviousYearMonth();

        var ok = FiskalySignTestPeriod.TryResolve(
            new FiskalySignTestRequest(),
            yearly: false,
            out var year,
            out var month,
            out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expectedYear, year);
        Assert.Equal(expectedMonth, month);
    }

    [Fact]
    public void TryResolve_CurrentMonth_Fails()
    {
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        var ok = FiskalySignTestPeriod.TryResolve(
            new FiskalySignTestRequest { Year = year, Month = month },
            yearly: false,
            out _,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Equal(FiskalySignTestPeriod.PeriodNotCompletedMessage, error);
    }
}
