using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Pricing;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Deterministik fiyat kuralı seçimi ve zaman penceresi birim testleri.
/// </summary>
public class PricingRuleEngineTests
{
    private static PricingRule R(
        Guid id,
        int priority,
        PricingRuleTargetScope scope,
        Guid targetId,
        PricingRuleActionType action,
        decimal actionValue,
        int daysMask = 0b1111111,
        bool timeWin = false,
        int tStart = 0,
        int tEnd = 1439,
        Guid? reg = null) =>
        new()
        {
            Id = id,
            Name = "t",
            Priority = priority,
            IsActive = true,
            ValidFromDate = new DateOnly(2025, 1, 1),
            ValidToDate = new DateOnly(2025, 12, 31),
            DaysOfWeekMask = daysMask,
            TimeWindowEnabled = timeWin,
            TimeStartMinutes = tStart,
            TimeEndMinutes = tEnd,
            TargetScope = scope,
            TargetId = targetId,
            ActionType = action,
            ActionValue = actionValue,
            CashRegisterId = reg,
            CreatedAtUtc = DateTime.UtcNow,
        };

    [Fact]
    public void PickAndApply_HigherPriorityWins()
    {
        var pid = Guid.NewGuid();
        var cid = Guid.NewGuid();
        var low = R(Guid.NewGuid(), 1, PricingRuleTargetScope.Product, pid, PricingRuleActionType.FixedGrossPrice, 5m);
        var high = R(Guid.NewGuid(), 10, PricingRuleTargetScope.Product, pid, PricingRuleActionType.FixedGrossPrice, 3m);
        var r = PricingRuleEngine.PickAndApply(new[] { low, high }, 10m, pid, cid);
        Assert.Equal(3m, r.UnitPriceGross);
        Assert.Equal(high.Id, r.AppliedRuleId);
    }

    [Fact]
    public void PickAndApply_ProductBeatsCategoryAtSamePriority()
    {
        var pid = Guid.NewGuid();
        var cat = Guid.NewGuid();
        var catRule = R(Guid.NewGuid(), 5, PricingRuleTargetScope.Category, cat, PricingRuleActionType.PercentOffList, 10m);
        var prodRule = R(Guid.NewGuid(), 5, PricingRuleTargetScope.Product, pid, PricingRuleActionType.PercentOffList, 20m);
        var r = PricingRuleEngine.PickAndApply(new[] { catRule, prodRule }, 10m, pid, cat);
        Assert.Equal(8m, r.UnitPriceGross);
        Assert.Equal(prodRule.Id, r.AppliedRuleId);
    }

    [Fact]
    public void IsMinuteInWindow_CrossMidnight()
    {
        Assert.True(PricingRuleEngine.IsMinuteInWindow(22 * 60, 2 * 60, 23 * 60));
        Assert.True(PricingRuleEngine.IsMinuteInWindow(22 * 60, 2 * 60, 90));
        Assert.False(PricingRuleEngine.IsMinuteInWindow(22 * 60, 2 * 60, 12 * 60));
    }

    [Fact]
    public void MatchesCalendarAndClock_RespectsDayMask()
    {
        var r = R(Guid.NewGuid(), 1, PricingRuleTargetScope.Product, Guid.NewGuid(), PricingRuleActionType.FixedGrossPrice, 1m, daysMask: 1 << (int)DayOfWeek.Monday);
        var monday = new DateOnly(2025, 3, 3);
        Assert.True(PricingRuleEngine.MatchesCalendarAndClock(r, monday, 1 << (int)DayOfWeek.Monday, 720));
        Assert.False(PricingRuleEngine.MatchesCalendarAndClock(r, monday.AddDays(1), 1 << (int)DayOfWeek.Tuesday, 720));
    }
}
