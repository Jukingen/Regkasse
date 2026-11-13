using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// FinanzOnline monthly <c>FIN_MONTHLY_{yyyyMM}_…</c> segment must follow
/// <see cref="PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm"/> (same as <see cref="Services.FinanzOnlineService.SubmitMonthlyClosingAsync"/>).
/// Guards against regressions to UTC calendar <c>ClosingDate</c> formatting.
/// </summary>
public sealed class FinanzOnlineMonthlyReferenceIdTests
{
    private static string BuildMonthlyReferenceId(DateTime closingDate, Guid cashRegisterId) =>
        $"FIN_MONTHLY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(closingDate)}_{cashRegisterId}";

    [Fact]
    public void MonthlyReference_YyyyMmSegment_UtcJanuaryInstant_ViennaFebruary_MonthBoundary()
    {
        var persistedUtc = new DateTime(2024, 1, 31, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal("202402", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(persistedUtc));
    }

    [Fact]
    public void MonthlyReference_YyyyMmSegment_UtcMonthDiffersFromViennaMonth()
    {
        var persistedUtc = new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc);
        Assert.Equal(12, persistedUtc.Month);
        Assert.Equal("202601", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(persistedUtc));
    }

    [Fact]
    public void MonthlyReference_PersistedClosingDateUtc_ViennaAnchorMidnight_YyyyMmMatchesViennaCalendarMonth()
    {
        var anchorUnspecified = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2025, 3, 1);
        var persisted = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(anchorUnspecified);
        Assert.Equal(DateTimeKind.Utc, persisted.Kind);
        Assert.Equal("202503", PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(persisted));
    }

    [Fact]
    public void MonthlyReference_FullReferenceId_MatchesFinanzOnlineServiceShape()
    {
        var cashRegisterId = Guid.Parse("a1b2c3d4-e5f6-4789-a012-3456789abcde");
        var persistedUtc = new DateTime(2024, 1, 31, 23, 30, 0, DateTimeKind.Utc);
        var id = BuildMonthlyReferenceId(persistedUtc, cashRegisterId);
        Assert.Equal($"FIN_MONTHLY_202402_{cashRegisterId}", id);
    }

    /// <summary>
    /// Pins the exact interpolation shape used by <c>SubmitMonthlyClosingAsync</c> (segment is Vienna <c>yyyyMM</c>, not UTC calendar month).
    /// </summary>
    [Fact]
    public void MonthlyReference_Regression_ServiceInterpolationPattern_ViennaYyyyMm_NotClosingDateUtcMonth()
    {
        var monthlyClosingClosingDate = new DateTime(2024, 1, 31, 23, 30, 0, DateTimeKind.Utc);
        var cashRegisterId = Guid.Parse("c0ffee00-0000-4000-8000-00000000beef");
        var referenceId =
            $"FIN_MONTHLY_{PostgreSqlUtcDateTime.FormatViennaUtcInstantAsYyyyMm(monthlyClosingClosingDate)}_{cashRegisterId}";
        Assert.Equal("FIN_MONTHLY_202402_c0ffee00-0000-4000-8000-00000000beef", referenceId);
        Assert.NotEqual(
            $"FIN_MONTHLY_{monthlyClosingClosingDate:yyyyMM}_{cashRegisterId}",
            referenceId);
    }
}
