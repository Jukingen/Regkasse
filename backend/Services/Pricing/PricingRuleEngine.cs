using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Pricing;

/// <summary>
/// Saf, deterministik kural seçimi ve fiyat uygulaması (unit test ve DB sonrası ortak mantık).
/// </summary>
public static class PricingRuleEngine
{
    /// <summary>
    /// Öncelik: Priority DESC, sonra ürün kapsamı kategori üzerinde, sonra Rule Id ASC (stabil bağ).
    /// </summary>
    public static PricingResolutionResult PickAndApply(
        IReadOnlyList<PricingRule> candidates,
        decimal catalogListPriceGross,
        Guid productId,
        Guid categoryId)
    {
        if (candidates.Count == 0)
            return new PricingResolutionResult(RoundGross(catalogListPriceGross), null);

        var ordered = candidates
            .Where(MatchesTarget(productId, categoryId))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.TargetScope == PricingRuleTargetScope.Product ? 1 : 0)
            .ThenBy(r => r.Id)
            .ToList();

        if (ordered.Count == 0)
            return new PricingResolutionResult(RoundGross(catalogListPriceGross), null);

        var winner = ordered[0];
        var unit = ApplyAction(winner, catalogListPriceGross);
        return new PricingResolutionResult(unit, winner.Id);
    }

    public static bool MatchesCalendarAndClock(
        PricingRule r,
        DateOnly localDate,
        int dayBit,
        int minutesFromMidnight)
    {
        if (r.ValidFromDate > localDate || r.ValidToDate < localDate)
            return false;
        if ((r.DaysOfWeekMask & dayBit) == 0)
            return false;
        if (!r.TimeWindowEnabled)
            return true;
        return IsMinuteInWindow(r.TimeStartMinutes, r.TimeEndMinutes, minutesFromMidnight);
    }

    /// <summary>
    /// Kasa kapsamı: null = tüm kasalar; doluysa yalnızca eşleşen kasa (istemci kasa göndermezse yalnızca global kurallar).</summary>
    public static bool MatchesCashRegister(PricingRule r, Guid? cashRegisterId)
    {
        if (!r.CashRegisterId.HasValue)
            return true;
        return cashRegisterId.HasValue && r.CashRegisterId == cashRegisterId.Value;
    }

    public static bool IsMinuteInWindow(int start, int end, int minutes)
    {
        start = Math.Clamp(start, 0, 1439);
        end = Math.Clamp(end, 0, 1439);
        minutes = Math.Clamp(minutes, 0, 1439);
        if (start <= end)
            return minutes >= start && minutes <= end;
        return minutes >= start || minutes <= end;
    }

    private static Func<PricingRule, bool> MatchesTarget(Guid productId, Guid categoryId) =>
        r => r.TargetScope switch
        {
            PricingRuleTargetScope.Product => r.TargetId == productId,
            PricingRuleTargetScope.Category => r.TargetId == categoryId,
            _ => false,
        };

    public static decimal ApplyAction(PricingRule rule, decimal catalogListPriceGross)
    {
        return rule.ActionType switch
        {
            PricingRuleActionType.FixedGrossPrice => RoundGross(rule.ActionValue),
            PricingRuleActionType.PercentOffList => RoundGross(catalogListPriceGross * (1m - Math.Clamp(rule.ActionValue, 0m, 100m) / 100m)),
            _ => RoundGross(catalogListPriceGross),
        };
    }

    public static decimal RoundGross(decimal v) =>
        Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
