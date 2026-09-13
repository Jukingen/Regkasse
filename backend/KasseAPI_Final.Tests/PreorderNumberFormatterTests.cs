using KasseAPI_Final.Services.Preorder;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PreorderNumberFormatterTests
{
    [Fact]
    public void Format_UsesViennaDateAndUnpaddedSequence()
    {
        var vienna = new DateTime(2026, 9, 20);
        Assert.Equal("BS260920", PreorderNumberFormatter.BuildPrefix(vienna));
        Assert.Equal("BS2609201", PreorderNumberFormatter.Format(vienna, 1));
    }
}
