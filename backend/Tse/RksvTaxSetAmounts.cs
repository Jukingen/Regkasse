namespace KasseAPI_Final.Tse;

/// <summary>
/// Gross amounts per RKSV tax bucket (Betrag-Satz-* fields).
/// </summary>
public sealed class RksvTaxSetAmounts
{
    public decimal Normal { get; init; }
    public decimal Ermaessigt1 { get; init; }
    public decimal Ermaessigt2 { get; init; }
    public decimal Null { get; init; }
    public decimal Besonders { get; init; }

    public decimal TotalGross => Normal + Ermaessigt1 + Ermaessigt2 + Null + Besonders;

    public long TotalGrossCents =>
        (long)Math.Round(Normal * 100m, 0, MidpointRounding.AwayFromZero)
        + (long)Math.Round(Ermaessigt1 * 100m, 0, MidpointRounding.AwayFromZero)
        + (long)Math.Round(Ermaessigt2 * 100m, 0, MidpointRounding.AwayFromZero)
        + (long)Math.Round(Null * 100m, 0, MidpointRounding.AwayFromZero)
        + (long)Math.Round(Besonders * 100m, 0, MidpointRounding.AwayFromZero);

    public static RksvTaxSetAmounts Zero => new();
}
