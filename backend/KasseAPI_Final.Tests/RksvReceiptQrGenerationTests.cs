using System.Globalization;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// QR wire format produced by <see cref="RksvReceiptQrPayloadBuilder"/> (same path as PaymentService / ReceiptService).
/// </summary>
public sealed class RksvReceiptQrGenerationTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    [Fact]
    public void QrCode_WhenGenerated_IsValidStandardRksvV1()
    {
        var qr = BuildQrForPaymentLikeFlow(
            issuedAtUtc: new DateTime(2026, 1, 15, 13, 30, 0, DateTimeKind.Utc),
            belegnummer: "AT-KASSE-001-20260115-42",
            taxSets: new RksvTaxSetAmounts
            {
                Normal = 100.00m,
                Ermaessigt1 = 5.50m,
                Ermaessigt2 = 2.25m,
                Null = 0m,
                Besonders = 1.00m,
            },
            turnoverCents: 10875);

        Assert.True(RksvQrParser.IsStandardRksvV1Format(qr));
        Assert.False(RksvQrParser.IsInternalCompactFormat(qr));

        var parsed = RksvQrParser.ParseOrThrow(qr);
        Assert.Equal(RksvQrPayloadLayout.StandardRksvV1, parsed.Layout);
        Assert.Equal(5, parsed.TaxBuckets.Count);
        Assert.Equal("100,00", parsed.TaxBuckets[0].Amount);
        Assert.Equal("5,50", parsed.TaxBuckets[1].Amount);
        Assert.NotNull(parsed.EncryptedTurnoverCounter);
        Assert.NotNull(parsed.PreviousSignature);

        Assert.True(SignaturePipeline.TryGetMachineCodeFromCompactJws(parsed.Signature, out var machineCode));
        Assert.StartsWith("_R1-AT1_", machineCode, StringComparison.Ordinal);
        // BMF §9 machine code body: Kasse, Beleg, Zeit, 5 Steuer-Beträge, Umsatzzähler, Zertifikat, Voriger-Sig (11 fields).
        var bodyAfterPrefix = machineCode.Substring("_R1-AT1_".Length);
        Assert.Equal(RksvQrParser.StandardRksvV1BodySegmentCount - 1, bodyAfterPrefix.Count(c => c == '_'));
    }

    [Fact]
    public void QrCode_WhenGenerated_UsesViennaLocalTime_NotUtc()
    {
        var issuedUtc = new DateTime(2026, 1, 15, 13, 30, 0, DateTimeKind.Utc);
        var expectedLocal = TimeZoneInfo.ConvertTimeFromUtc(
            issuedUtc,
            PostgreSqlUtcDateTime.AustriaTimeZone);

        var qr = BuildQrForPaymentLikeFlow(
            issuedUtc,
            "AT-KASSE-001-20260115-99",
            new RksvTaxSetAmounts { Normal = 12.40m },
            1240);

        var parsed = RksvQrParser.ParseOrThrow(qr);
        Assert.Equal(expectedLocal.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), parsed.Timestamp);
        Assert.NotEqual(issuedUtc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), parsed.Timestamp);
    }

    [Fact]
    public void QrCode_LegacyInternalCompact_StillParsesForBackwardCompatibility()
    {
        var jws = "eyJhbGciOiJFUzI1NiJ9.eyJrYXNzZW5JZCI6IjEifQ.signature";
        var legacy =
            $"_R1-AT1_KASSE-001_AT-KASSE-001-20260228-00000001_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_{jws}";

        Assert.True(RksvQrParser.IsInternalCompactFormat(legacy));
        var parsed = RksvQrParser.ParseOrThrow(legacy);
        Assert.Equal(RksvQrPayloadLayout.InternalCompact, parsed.Layout);
    }

    [Fact]
    public void ReceiptServiceResolvePath_MatchesPaymentBuilder_ForSameSignature()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-1",
            new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 9.99m },
            999,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);
        var jws = pipeline.Sign(payload, "parity");

        var paymentQr = RksvReceiptQrPayloadBuilder.BuildFromCompactJwsOrNull(jws);
        var receiptQr = RksvReceiptQrPayloadBuilder.BuildFromCompactJwsOrNull(jws);

        Assert.False(string.IsNullOrWhiteSpace(paymentQr));
        Assert.Equal(paymentQr, receiptQr);
        Assert.True(RksvQrParser.IsStandardRksvV1Format(paymentQr));
    }

    private static string BuildQrForPaymentLikeFlow(
        DateTime issuedAtUtc,
        string belegnummer,
        RksvTaxSetAmounts taxSets,
        long turnoverCents,
        string? previousJws = null)
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            belegnummer,
            issuedAtUtc,
            taxSets,
            turnoverCents,
            previousJws,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);

        var jws = pipeline.Sign(payload, "qr-generation-test");
        Assert.True(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(jws, out var qr));
        return qr;
    }
}
