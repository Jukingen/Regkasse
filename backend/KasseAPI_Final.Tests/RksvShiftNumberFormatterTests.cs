using KasseAPI_Final.Services.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvShiftNumberFormatterTests
{
    [Fact]
    public void Format_ReturnsFirstEightHexChars_WhenShiftIdPresent()
    {
        var shiftId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        Assert.Equal("12345678", RksvShiftNumberFormatter.Format(shiftId));
    }

    [Fact]
    public void FormatOrDash_ReturnsDash_WhenMissing()
    {
        Assert.Equal("—", RksvShiftNumberFormatter.FormatOrDash((Guid?)null));
    }
}
