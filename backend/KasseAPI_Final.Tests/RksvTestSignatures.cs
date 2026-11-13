using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;

namespace KasseAPI_Final.Tests;

/// <summary>Software-TSE compact JWS samples that <see cref="RksvReceiptQrPayloadBuilder"/> can turn into §9 QR.</summary>
internal static class RksvTestSignatures
{
    public static string CreateDemoCompactJws(
        string kassenId = "KASSE-01",
        string belegNr = "AT-KASSE-01-TEST-1")
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var aes = keyProvider.GetTurnoverCounterAesKeyBytes()
            ?? throw new InvalidOperationException("Software TSE AES key missing.");
        var payload = BelegdatenPayloadBuilder.Build(
            kassenId,
            belegNr,
            DateTime.UtcNow,
            RksvTaxSetAmounts.Zero,
            turnoverCounterCents: 0,
            previousCompactJws: null,
            keyProvider.GetCertificateSerialNumber()!,
            aes);
        return pipeline.Sign(payload, "rksv-test");
    }
}
