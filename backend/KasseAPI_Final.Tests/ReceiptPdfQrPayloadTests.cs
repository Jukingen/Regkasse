using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReceiptPdfQrPayloadTests
{
    [Fact]
    public void ResolveQrPayloadForPdf_UsesCompactJwsWhenStoredPayloadIsEmpty()
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
            keyProvider.GetTurnoverCounterAesKeyBytes()!);

        var jws = pipeline.Sign(payload, "pdf-qr");
        var qr = ReceiptPdfService.ResolveQrPayloadForPdf(storedPayload: null, signatureValue: jws, tseSignature: null);

        Assert.False(string.IsNullOrEmpty(qr));
        Assert.StartsWith("_R1-AT", qr);
        Assert.DoesNotContain('.', qr);
        Assert.True(RksvQrParser.Parse(qr).Success);
    }

    [Fact]
    public void ResolveQrPayloadForPdf_KeepsStoredMachineCodeWhenSignatureIsMissing()
    {
        const string stored = "_R1-AT1_KASSE_1_2_3_4_5_6_7_8_9_10_11";
        var qr = ReceiptPdfService.ResolveQrPayloadForPdf(stored, signatureValue: null, tseSignature: null);
        Assert.Equal(stored, qr);
    }
}
