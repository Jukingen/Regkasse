using KasseAPI_Final.Models.Export;
using Xunit;

namespace KasseAPI_Final.Tests;

public class FiscalExportChainContinuityTests
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
        var links = new List<FiscalReceiptChainLinkDto>
        {
            new() { ReceiptNumber = "1", SignatureValue = "sig1", PrevSignatureValue = "" },
            new() { ReceiptNumber = "2", SignatureValue = "sig2", PrevSignatureValue = "sig1" },
            new() { ReceiptNumber = "3", SignatureValue = "sig3", PrevSignatureValue = "sig2" }
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
