using KasseAPI_Final.Tse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FakeTseProviderTests
{
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
        var payload = new BelegdatenPayload
        {
            KassenId = "K1",
            BelegNr = "MONTHLY_202503",
            BelegDatum = "01.03.2025",
            Uhrzeit = "23:59:59",
            Betrag = "100.00",
            PrevSignatureValue = "prev-chain",
            TaxDetails = "{}"
        };

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
        var p1 = new BelegdatenPayload
        {
            KassenId = "K1",
            BelegNr = "Y",
            BelegDatum = "01.01.2025",
            Uhrzeit = "23:59:59",
            Betrag = "1.00",
            PrevSignatureValue = "A",
            TaxDetails = "{}"
        };
        var p2 = new BelegdatenPayload { KassenId = p1.KassenId, BelegNr = p1.BelegNr, BelegDatum = p1.BelegDatum, Uhrzeit = p1.Uhrzeit, Betrag = p1.Betrag, PrevSignatureValue = "B", TaxDetails = "{}" };

        var j1 = FakeTseProvider.BuildDeterministicPseudoJws(p1, "c");
        var j2 = FakeTseProvider.BuildDeterministicPseudoJws(p2, "c");
        Assert.NotEqual(j1, j2);
    }
}
