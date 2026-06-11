using KasseAPI_Final.Time;

namespace KasseAPI_Final.Tse;

/// <summary>
/// Builds RKSV §9 Belegdaten for TSE signing from register / payment context.
/// </summary>
public static class BelegdatenPayloadBuilder
{
    public static BelegdatenPayload Build(
        string kassenId,
        string belegnummer,
        DateTime timestampUtc,
        RksvTaxSetAmounts taxSets,
        long turnoverCounterCents,
        string? previousCompactJws,
        string certificateSerialNumber,
        byte[] turnoverAesKey,
        bool updateTurnoverCounter = true)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc),
            PostgreSqlUtcDateTime.AustriaTimeZone);

        var encryptedTurnover = updateTurnoverCounter
            ? RksvTurnoverCounterCrypto.Encrypt(turnoverCounterCents, kassenId, belegnummer, turnoverAesKey)
            : RksvTurnoverCounterCrypto.Encrypt(0, kassenId, belegnummer, turnoverAesKey);

        return new BelegdatenPayload
        {
            KassenId = kassenId,
            Belegnummer = belegnummer,
            BelegDatumUhrzeit = local.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            BetragSatzNormal = taxSets.Normal,
            BetragSatzErmaessigt1 = taxSets.Ermaessigt1,
            BetragSatzErmaessigt2 = taxSets.Ermaessigt2,
            BetragSatzNull = taxSets.Null,
            BetragSatzBesonders = taxSets.Besonders,
            StandUmsatzZaehlerAes256Icm = encryptedTurnover,
            ZertifikatSeriennummer = certificateSerialNumber,
            SigVorigerBeleg = RksvChainingValue.Compute(previousCompactJws, kassenId),
        };
    }

    public static RksvTaxSetAmounts MapTaxSets(string? taxDetailsJson, decimal totalAmount) =>
        RksvTaxSetMapper.MapFromTaxDetailsJson(taxDetailsJson, totalAmount);
}
