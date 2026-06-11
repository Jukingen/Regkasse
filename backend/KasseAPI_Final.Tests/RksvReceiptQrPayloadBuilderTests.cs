using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvReceiptQrPayloadBuilderTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    [Fact]
    public void TryBuildFromCompactJws_ProducesStandardRksvV1QrWireFormat()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-42",
            new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);

        var jws = pipeline.Sign(payload, "qr-test");
        Assert.True(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(jws, out var qr));

        var parsed = RksvQrParser.Parse(qr);
        Assert.True(parsed.Success);
        Assert.Equal(RksvQrPayloadLayout.StandardRksvV1, parsed.Payload!.Layout);
        Assert.Equal(jws, parsed.Payload.Signature);
        Assert.Equal("100,00", parsed.Payload.TaxBuckets[0].Amount);
        Assert.NotNull(parsed.Payload.EncryptedTurnoverCounter);
        Assert.NotNull(parsed.Payload.PreviousSignature);
    }

    [Fact]
    public void TryBuildFromCompactJws_InvalidInput_ReturnsFalse()
    {
        Assert.False(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(null, out _));
        Assert.False(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws("a.b", out _));
        Assert.False(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws("a.b.c.d", out _));
    }
}
