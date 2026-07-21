using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseChainContinuityAnalyzerTests
{
    [Theory]
    [InlineData("AT-K1-20260527-1", 1, "20260527", true)]
    [InlineData("AT-K1-20260527-42", 42, "20260527", true)]
    [InlineData("INVALID", 0, null, false)]
    public void TryParseBelegNrSequence_ParsesAtFormat(string belegNr, int seq, string? ymd, bool ok)
    {
        var parsed = TseChainContinuityAnalyzer.TryParseBelegNrSequence(belegNr, out var sequence, out var dateYmd);
        Assert.Equal(ok, parsed);
        if (ok)
        {
            Assert.Equal(seq, sequence);
            Assert.Equal(ymd, dateYmd);
        }
    }

    [Fact]
    public void AnalyzeRegister_DetectsChainBreakAndSequenceGap()
    {
        var regId = Guid.NewGuid();
        var t0 = DateTime.UtcNow.AddHours(-2);
        var links = new List<TseChainContinuityAnalyzer.ReceiptLink>
        {
            new()
            {
                ReceiptId = Guid.NewGuid(),
                ReceiptNumber = "AT-K1-20260527-1",
                CreatedAtUtc = t0,
                SignatureValue = "sig-a",
                PrevSignatureValue = "",
                ParsedSequence = 1,
                ParsedSequenceDateYmd = "20260527",
            },
            new()
            {
                ReceiptId = Guid.NewGuid(),
                ReceiptNumber = "AT-K1-20260527-3",
                CreatedAtUtc = t0.AddMinutes(5),
                SignatureValue = "sig-c",
                PrevSignatureValue = "wrong-prev-not-rksv-chain",
                ParsedSequence = 3,
                ParsedSequenceDateYmd = "20260527",
            },
        };

        var report = TseChainContinuityAnalyzer.AnalyzeRegister(
            regId,
            "K1",
            new DateTime(2026, 5, 27),
            new DateTime(2026, 5, 27),
            links,
            lastCounterFromState: 3);

        Assert.Equal(2, report.SignatureCount);
        Assert.True(report.HasGaps);
        Assert.Equal(1, report.ChainBreakCount);
        Assert.Equal(1, report.SequenceGapCount);
        Assert.Contains("export", report.DetailsExportPath, StringComparison.OrdinalIgnoreCase);
    }
}
