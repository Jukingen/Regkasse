using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Pricing;

/// <summary>
/// Veritabanından aday kuralları yükleyip <see cref="PricingRuleEngine"/> ile çözüm üretir.
/// </summary>
public sealed class PricingRuleResolver : IPricingRuleResolver
{
    private readonly AppDbContext _db;

    public PricingRuleResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PricingResolutionResult> ResolveUnitGrossAsync(
        decimal catalogListPriceGross,
        Guid productId,
        Guid categoryId,
        Guid? cashRegisterId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var tz = PostgreSqlUtcDateTime.AustriaTimeZone;
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
        var localDate = DateOnly.FromDateTime(local.Date);
        var minutes = local.Hour * 60 + local.Minute;
        var dayBit = 1 << (int)local.DayOfWeek;

        var rules = await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.IsActive && r.ValidFromDate <= localDate && r.ValidToDate >= localDate)
            .ToListAsync(ct);

        var candidates = rules
            .Where(r => PricingRuleEngine.MatchesCalendarAndClock(r, localDate, dayBit, minutes))
            .Where(r => PricingRuleEngine.MatchesCashRegister(r, cashRegisterId))
            .ToList();

        return PricingRuleEngine.PickAndApply(candidates, catalogListPriceGross, productId, categoryId);
    }
}
