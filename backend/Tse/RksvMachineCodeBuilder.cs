using System.Globalization;

namespace KasseAPI_Final.Tse;

/// <summary>
/// RKSV Detailspezifikation Abs. 5 — compressed machine-readable signing input.
/// </summary>
public static class RksvMachineCodeBuilder
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    public static string BuildDataToBeSigned(BelegdatenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return string.Join("_",
            "_" + RksvSuite.SuiteId,
            payload.KassenId,
            payload.Belegnummer,
            payload.BelegDatumUhrzeit,
            FormatGermanAmount(payload.BetragSatzNormal),
            FormatGermanAmount(payload.BetragSatzErmaessigt1),
            FormatGermanAmount(payload.BetragSatzErmaessigt2),
            FormatGermanAmount(payload.BetragSatzNull),
            FormatGermanAmount(payload.BetragSatzBesonders),
            payload.StandUmsatzZaehlerAes256Icm,
            payload.ZertifikatSeriennummer,
            payload.SigVorigerBeleg);
    }

    internal static string FormatGermanAmount(decimal amount)
    {
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.00", GermanCulture);
    }
}
