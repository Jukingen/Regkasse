using KasseAPI_Final.Tse;
using Xunit;

namespace KasseAPI_Final.Tests.CountryBaseline;

/// <summary>
/// Regression guard for the Austrian fiscal chain. Captured while AT is the only supported regime,
/// so that any future country/tax/invoice abstraction can be proven not to have moved AT output.
/// </summary>
public sealed class AtFiscalChainBaselineTests
{
    private const string FixtureFileName = "at-fiscal-chain.baseline.json";

    [Fact]
    public void AtFiscalChain_MatchesCommittedBaseline()
    {
        var capture = AtFiscalChainBaseline.Capture();

        BaselineFixtureFile.AssertMatchesFixture(FixtureFileName, capture.Snapshot);
    }

    [Fact]
    public void AtFiscalChain_IsReproducibleAcrossRuns()
    {
        var first = BaselineFixtureFile.Serialize(AtFiscalChainBaseline.Capture().Snapshot);
        var second = BaselineFixtureFile.Serialize(AtFiscalChainBaseline.Capture().Snapshot);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// The signature segment is masked in the fixture because ECDSA nonces are random.
    /// It still has to be a real ES256 signature over the frozen signing input.
    /// </summary>
    [Fact]
    public void AtFiscalChain_SignaturesVerifyAgainstFrozenSigningInput()
    {
        var capture = AtFiscalChainBaseline.Capture();
        var pipeline = new SignaturePipeline(
            capture.KeyProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SignaturePipeline>.Instance);
        var publicKey = capture.KeyProvider.GetPublicKey();

        Assert.NotEmpty(capture.SignedReceipts);
        foreach (var receipt in capture.SignedReceipts)
        {
            Assert.True(
                pipeline.Verify(receipt.CompactJws, publicKey),
                $"ES256 verification failed for baseline step '{receipt.Step}'.");

            Assert.Equal($"{receipt.SigningInput}.{receipt.SignatureSegment}", receipt.CompactJws);
        }
    }

    /// <summary>Raw R||S is 64 bytes → 86 Base64URL characters, no padding (RKSV checklist 4–5).</summary>
    [Fact]
    public void AtFiscalChain_SignatureSegmentsKeepEs256WireShape()
    {
        var capture = AtFiscalChainBaseline.Capture();

        foreach (var receipt in capture.SignedReceipts)
        {
            Assert.Equal(86, receipt.SignatureSegment.Length);
            Assert.DoesNotContain('=', receipt.SignatureSegment);
            Assert.DoesNotContain('+', receipt.SignatureSegment);
            Assert.DoesNotContain('/', receipt.SignatureSegment);
            Assert.Equal(64, TseCryptoHelper.FromBase64UrlNoPadding(receipt.SignatureSegment).Length);
        }
    }

    /// <summary>Every signed payload must stay BMF §9 machine code (<c>_R1-…</c>), not legacy JSON.</summary>
    [Fact]
    public void AtFiscalChain_StaysF5CompliantMachineCode()
    {
        var capture = AtFiscalChainBaseline.Capture();

        foreach (var receipt in capture.SignedReceipts)
        {
            Assert.True(
                SignaturePipeline.IsF5CompliantJws(receipt.CompactJws),
                $"Baseline step '{receipt.Step}' is not F5-compliant RKSV machine code.");
        }
    }

    /// <summary>
    /// Sig-Voriger-Beleg hashes the previous receipt's signature, so a live chain cannot be
    /// byte-frozen. Linkage is asserted structurally instead.
    /// </summary>
    [Fact]
    public void AtFiscalChain_KeepsUnbrokenChainingWhenSignedSequentially()
    {
        var kassenId = AtFiscalChainBaseline.KassenIdForChaining;
        var links = AtFiscalChainBaseline.BuildLiveChain();

        Assert.NotEmpty(links);
        Assert.Null(links[0].PredecessorJws);
        Assert.Equal(RksvChainingValue.Compute(null, kassenId), links[0].SigVorigerBeleg);

        for (var i = 1; i < links.Count; i++)
        {
            Assert.Equal(links[i - 1].CompactJws, links[i].PredecessorJws);
            Assert.Equal(
                RksvChainingValue.Compute(links[i - 1].CompactJws, kassenId),
                links[i].SigVorigerBeleg);
        }

        Assert.Equal(links.Count, links.Select(l => l.SigVorigerBeleg).Distinct().Count());
    }
}
