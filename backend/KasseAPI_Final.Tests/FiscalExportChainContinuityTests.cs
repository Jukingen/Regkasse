using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Tse;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalExportChainContinuityTests
{
    [Fact]
    public void BuildWarnings_EmptyOrSingle_ReturnsEmpty()
    {
        Assert.Empty(FiscalExportChainContinuity.BuildWarnings(Array.Empty<FiscalReceiptChainLinkDto>()));
        Assert.Empty(FiscalExportChainContinuity.BuildWarnings(new List<FiscalReceiptChainLinkDto>
        {
            new() { ReceiptNumber = "A", SignatureValue = "s1", PrevSignatureValue = "" }
        }));
    }

    [Fact]
    public void BuildWarnings_ContinuousChain_ReturnsEmpty()
    {
        const string sig1 = "eyJhbGci.eyJkYXRh.c2ln";
        const string sig2 = "eyJhbGci.eyJkYXRh.c2lnMg";
        var links = new List<FiscalReceiptChainLinkDto>
        {
            new() { ReceiptNumber = "1", SignatureValue = sig1, PrevSignatureValue = "" },
            new() { ReceiptNumber = "2", SignatureValue = sig2, PrevSignatureValue = RksvChainingValue.Compute(sig1, string.Empty) },
            new() { ReceiptNumber = "3", SignatureValue = "sig3", PrevSignatureValue = RksvChainingValue.Compute(sig2, string.Empty) }
        };
        Assert.Empty(FiscalExportChainContinuity.BuildWarnings(links));
    }

    [Fact]
    public void BuildWarnings_Break_ReturnsWarning()
    {
        var links = new List<FiscalReceiptChainLinkDto>
        {
            new() { ReceiptNumber = "1", SignatureValue = "sig1", PrevSignatureValue = "" },
            new() { ReceiptNumber = "2", SignatureValue = "sig2", PrevSignatureValue = "wrong" }
        };
        var w = FiscalExportChainContinuity.BuildWarnings(links);
        Assert.Single(w);
        Assert.Contains("2", w[0], StringComparison.Ordinal);
    }
}
