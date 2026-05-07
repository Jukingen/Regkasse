using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class DisclaimerServiceTests
{
    private readonly DisclaimerService _sut = new();

    [Fact]
    public void GetRksvDisclaimer_DefaultAndDe_ReturnGerman()
    {
        Assert.Equal(DisclaimerService.RksvDisclaimerDe, _sut.GetRksvDisclaimer("de"));
        Assert.Equal(DisclaimerService.RksvDisclaimerDe, _sut.GetRksvDisclaimer(""));
        Assert.Equal(DisclaimerService.RksvDisclaimerDe, _sut.GetRksvDisclaimer("  "));
    }

    [Fact]
    public void GetRksvDisclaimer_En_ReturnsEnglish()
    {
        Assert.Equal(DisclaimerService.RksvDisclaimerEn, _sut.GetRksvDisclaimer("en"));
        Assert.Equal(DisclaimerService.RksvDisclaimerEn, _sut.GetRksvDisclaimer("EN"));
    }
}
