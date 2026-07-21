using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FakeTseProviderTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    private static BelegdatenPayload BuildPayload(string prevChain = "prev-chain") =>
        BelegdatenPayloadBuilder.Build(
            "K1",
            "MONTHLY_202503",
            new DateTime(2025, 3, 1, 22, 59, 59, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            "SIM-TEST",
            DevAesKey);

    [Fact]
    public async Task IsReadyAsync_AlwaysTrue()
    {
        var p = new FakeTseProvider(NullLogger<FakeTseProvider>.Instance);
        Assert.True(await p.IsReadyAsync());
    }

    [Fact]
    public async Task SignAsync_IsDeterministic_AndProducesLongCompactString()
    {
        var p = new FakeTseProvider(NullLogger<FakeTseProvider>.Instance);
        var payload = BuildPayload();

        var a = await p.SignAsync(payload, "corr123");
        var b = await p.SignAsync(payload, "corr123");

        Assert.Equal(FakeTseProvider.FakeCertificateSerial, a.CertificateSerialNumber);
        Assert.Equal(a.CompactJws, b.CompactJws);
        Assert.Contains('.', a.CompactJws);
        var parts = a.CompactJws.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.True(a.CompactJws.Length > 500, "Fake JWS should exceed legacy varchar(500) to validate DB text columns.");
    }

    [Fact]
    public void BuildDeterministicPseudoJws_ChangesWithPrevSignature()
    {
        _ = new SoftwareTseKeyProvider();
        var p1 = BelegdatenPayloadBuilder.Build(
            "K1", "Y", new DateTime(2025, 1, 1, 22, 59, 59, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 1.00m }, 100, null, "SIM", DevAesKey);
        var p2 = BelegdatenPayloadBuilder.Build(
            "K1", "Y", new DateTime(2025, 1, 1, 22, 59, 59, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 1.00m }, 100, "prev-jws", "SIM", DevAesKey);

        var j1 = FakeTseProvider.BuildDeterministicPseudoJws(p1, "c");
        var j2 = FakeTseProvider.BuildDeterministicPseudoJws(p2, "c");
        Assert.NotEqual(j1, j2);
    }
}
