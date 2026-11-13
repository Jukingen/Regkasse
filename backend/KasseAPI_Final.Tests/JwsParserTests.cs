using KasseAPI_Final.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class JwsParserTests
{
    /// <summary>BMF / fiskaly-style RKSV QR: comma amounts, standard Base64 Sig-Wert with padding.</summary>
    private const string FiskalyAt3Sample =
        "_R1-AT3_dGxx_19_2017-10-24T11:07:32_0,00_0,00_0,00_0,00_0,00_7eti9M9dETz2_5474185F_M8LJDeWizNY=_4CtUHTuHoWvNfY0Ty+K8SuUVPYfZHjkM70/ZzATkb7Oj6G8PNWR6K1vsFWTXg2YsMyYHxVXpGJYEiAn0Uojfzw==";

    [Fact]
    public void Parse_FiskalyMachineCode_ReconstructsCompactJwsWithoutPadding()
    {
        var result = JwsParser.Parse(FiskalyAt3Sample);

        Assert.True(result.Success, result.Error);
        Assert.Equal(JwsWireFormat.FiskalyMachineCode, result.Format);
        var parts = result.CompactJws.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.DoesNotContain('=', result.CompactJws);
        Assert.DoesNotContain('+', result.CompactJws);
        Assert.DoesNotContain('/', result.CompactJws);

        var sigBytes = TseCryptoHelper.FromBase64UrlNoPadding(parts[2]);
        Assert.Equal(64, sigBytes.Length);

        var payload = System.Text.Encoding.UTF8.GetString(TseCryptoHelper.FromBase64UrlNoPadding(parts[1]));
        Assert.StartsWith("_R1-AT3_", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("4CtUHTuHoWvNfY0Ty", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CompactJws_RoundTrips()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, new Mock<ILogger<SignaturePipeline>>().Object);
        var jws = pipeline.Sign(SamplePayload(keyProvider));

        var result = JwsParser.Parse(jws);

        Assert.True(result.Success, result.Error);
        Assert.Equal(JwsWireFormat.CompactJws, result.Format);
        Assert.Equal(jws, result.CompactJws);
    }

    [Fact]
    public void Parse_RksvQrWire_ReconstructsCompactJwsFromSigWert()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, new Mock<ILogger<SignaturePipeline>>().Object);
        var jws = pipeline.Sign(SamplePayload(keyProvider));
        Assert.True(RksvReceiptQrPayloadBuilder.TryBuildFromCompactJws(jws, out var qr));

        var result = JwsParser.Parse(qr);

        Assert.True(result.Success, result.Error);
        Assert.Equal(JwsWireFormat.FiskalyMachineCode, result.Format);
        Assert.Equal(jws, result.CompactJws);
    }

    [Fact]
    public void Parse_Garbage_FailsWithPartsCount()
    {
        var result = JwsParser.Parse("not-a-jws");
        Assert.False(result.Success);
        Assert.Contains("3 parts", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromBase64UrlOrStd_AcceptsStandardBase64WithPadding()
    {
        var original = new byte[] { 1, 2, 3, 4, 5 };
        var std = Convert.ToBase64String(original);
        Assert.Contains('=', std);

        var decoded = TseCryptoHelper.FromBase64UrlOrStd(std);
        Assert.Equal(original, decoded);

        var url = TseCryptoHelper.NormalizeToBase64UrlNoPadding(std);
        Assert.DoesNotContain('=', url);
        Assert.Equal(original, TseCryptoHelper.FromBase64UrlNoPadding(url));
    }

    private static BelegdatenPayload SamplePayload(SoftwareTseKeyProvider keyProvider) =>
        BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE001-20250225-12345678",
            new DateTime(2025, 2, 25, 13, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 123.45m },
            12345,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            keyProvider.GetTurnoverCounterAesKeyBytes()!);
}
