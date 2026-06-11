using KasseAPI_Final.Tse;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BelegdatenPayloadTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    [Fact]
    public void Build_StartReceipt_MachineCodeUsesGermanDecimalsAndRksvFields()
    {
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-1",
            new DateTime(2026, 1, 15, 13, 23, 55, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 12.40m },
            turnoverCounterCents: 1240,
            previousCompactJws: null,
            certificateSerialNumber: "CERT1",
            DevAesKey);

        Assert.Equal("KASSE-001", payload.KassenId);
        Assert.Contains("T", payload.BelegDatumUhrzeit);
        Assert.Equal(12.40m, payload.BetragSatzNormal);
        Assert.NotEmpty(payload.StandUmsatzZaehlerAes256Icm);
        Assert.NotEmpty(payload.SigVorigerBeleg);

        var machineCode = RksvMachineCodeBuilder.BuildDataToBeSigned(payload);
        Assert.StartsWith("_R1-AT1_KASSE-001_AT-KASSE-001-20260115-1_", machineCode);
        Assert.Contains("_12,40_", machineCode);
    }

    [Fact]
    public void ChainingValue_FirstReceipt_HashesKassenId()
    {
        var chain = RksvChainingValue.Compute(null, "KASSE-001");
        Assert.NotEmpty(chain);
        Assert.Equal(12, chain.Length);
    }

    [Fact]
    public void ChainingValue_SubsequentReceipt_HashesPreviousJws()
    {
        var jws = "eyJhbGci.eyJkYXRh.c2ln";
        var chain = RksvChainingValue.Compute(jws, "KASSE-001");
        Assert.NotEqual(RksvChainingValue.Compute(null, "KASSE-001"), chain);
    }

    [Fact]
    public void TurnoverCounter_EncryptDecrypt_RoundTrip()
    {
        const string kassen = "K1";
        const string beleg = "AT-K1-20260115-1";
        const long cents = 12345;

        var enc = RksvTurnoverCounterCrypto.Encrypt(cents, kassen, beleg, DevAesKey);
        var dec = RksvTurnoverCounterCrypto.Decrypt(enc, kassen, beleg, DevAesKey);
        Assert.Equal(cents, dec);
    }

    [Fact]
    public void TaxSetMapper_MapsTaxTypeAmountsToGrossBuckets()
    {
        var json = """{"1":2.00,"2":1.00}""";
        var sets = RksvTaxSetMapper.MapFromTaxDetailsJson(json, 0m);
        Assert.Equal(12.00m, sets.Normal);
        Assert.Equal(11.00m, sets.Ermaessigt1);
    }

    [Fact]
    public void SignaturePipeline_SignsRksvMachineCodePayload()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<SignaturePipeline>>());

        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-42",
            new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);

        var jws = pipeline.Sign(payload, "test");
        Assert.Equal(3, jws.Split('.').Length);
        Assert.True(pipeline.Verify(jws, keyProvider.GetPublicKey()));
    }
}
