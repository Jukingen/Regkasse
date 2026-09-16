using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AutoMonatsbelegCutoffTests
{
    [Fact]
    public void IsInAutoCreateWindow_Day1_Before0001_IsFalse()
    {
        var vienna = new DateTime(2026, 9, 1, 0, 0, 30);
        Assert.False(AutoMonatsbelegCutoff.IsInAutoCreateWindow(vienna));
    }

    [Fact]
    public void IsInAutoCreateWindow_Day1_At0001_IsTrue()
    {
        var vienna = new DateTime(2026, 9, 1, 0, 1, 0);
        Assert.True(AutoMonatsbelegCutoff.IsInAutoCreateWindow(vienna));
    }

    [Fact]
    public void IsInAutoCreateWindow_Day7_IsTrue_Day8_IsFalse()
    {
        Assert.True(AutoMonatsbelegCutoff.IsInAutoCreateWindow(new DateTime(2026, 9, 7, 12, 0, 0)));
        Assert.False(AutoMonatsbelegCutoff.IsInAutoCreateWindow(new DateTime(2026, 9, 8, 12, 0, 0)));
    }

    [Fact]
    public void PreviousMonthAnchor_January_IsDecemberPriorYear()
    {
        var prev = AutoMonatsbelegCutoff.PreviousMonthAnchor(new DateTime(2026, 1, 1, 0, 1, 0));
        Assert.Equal(2025, prev.Year);
        Assert.Equal(12, prev.Month);
    }

    [Fact]
    public void JahresbelegFonDeadline_Is15FebruaryNextYear()
    {
        Assert.Equal(new DateTime(2027, 2, 15), AutoMonatsbelegCutoff.JahresbelegFonDeadline(2026));
    }

    [Fact]
    public void GetDelayUntilNextSweep_Day1Before0001_WaitsUntil0001()
    {
        // 2026-08-31 22:00 UTC = 2026-09-01 00:00 Vienna (CEST)
        var utc = new DateTime(2026, 8, 31, 22, 0, 0, DateTimeKind.Utc);
        var delay = AutoMonatsbelegCutoff.GetDelayUntilNextSweep(utc, intervalMinutes: 15, allowImmediateIfInWindow: true);
        Assert.Equal(TimeSpan.FromMinutes(1), delay);
    }

    [Fact]
    public void GetDelayUntilNextSweep_InWindow_ImmediateOnFirstTick()
    {
        // 2026-08-31 22:05 UTC = 2026-09-01 00:05 Vienna
        var utc = new DateTime(2026, 8, 31, 22, 5, 0, DateTimeKind.Utc);
        var delay = AutoMonatsbelegCutoff.GetDelayUntilNextSweep(utc, 15, allowImmediateIfInWindow: true);
        Assert.Equal(TimeSpan.Zero, delay);
        var later = AutoMonatsbelegCutoff.GetDelayUntilNextSweep(utc, 15, allowImmediateIfInWindow: false);
        Assert.Equal(TimeSpan.FromMinutes(15), later);
    }

    [Fact]
    public void MaskTseSignature_TruncatesCompactJws()
    {
        var jws = "aaaaaaaa.bbbbbbbbbbbb.cccccccc";
        Assert.Equal("aaaaaaaa…cccccccc", AutoMonatsbelegCutoff.MaskTseSignature(jws));
        Assert.Equal("InDep", AutoMonatsbelegCutoff.DepStatus(jws));
        Assert.Equal("Missing", AutoMonatsbelegCutoff.DepStatus("nope"));
    }

    [Fact]
    public void ClampRetryCount_Bounds()
    {
        Assert.Equal(1, AutoMonatsbelegCutoff.ClampRetryCount(0));
        Assert.Equal(5, AutoMonatsbelegCutoff.ClampRetryCount(99));
        Assert.Equal(3, AutoMonatsbelegCutoff.ClampRetryCount(3));
    }
}
