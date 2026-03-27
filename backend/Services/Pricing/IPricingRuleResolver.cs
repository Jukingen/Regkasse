namespace KasseAPI_Final.Services.Pricing;

/// <summary>
/// Katalog brüt liste fiyatından, aktif kurallara göre birim brüt fiyat çözümü (tek kazanan kural).
/// </summary>
public interface IPricingRuleResolver
{
    Task<PricingResolutionResult> ResolveUnitGrossAsync(
        decimal catalogListPriceGross,
        Guid productId,
        Guid categoryId,
        Guid? cashRegisterId,
        DateTime utcNow,
        CancellationToken ct = default);
}

/// <param name="UnitPriceGross">Yuvarlanmış brüt birim fiyat (2 dp).</param>
/// <param name="AppliedRuleId">Kural uygulanmadıysa null.</param>
public sealed record PricingResolutionResult(decimal UnitPriceGross, Guid? AppliedRuleId);
