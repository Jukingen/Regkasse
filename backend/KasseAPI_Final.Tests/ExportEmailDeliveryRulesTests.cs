using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ExportEmailDeliveryRulesTests
{
    [Fact]
    public void ShouldUseLink_when_over_attachment_cap()
    {
        Assert.True(ExportEmailDeliveryRules.ShouldUseLink(11 * 1024 * 1024, 10 * 1024 * 1024, preferLink: false));
        Assert.False(ExportEmailDeliveryRules.ShouldUseLink(1024, 10 * 1024 * 1024, preferLink: false));
        Assert.True(ExportEmailDeliveryRules.ShouldUseLink(100, 10 * 1024 * 1024, preferLink: true));
    }

    [Fact]
    public void BuildPublicDownloadLink_joins_base_and_token()
    {
        var opts = new ExportEmailOptions
        {
            PublicApiBaseUrl = "https://api.regkasse.at/",
            DownloadPathTemplate = "data/export-email/{token}",
        };
        var link = ExportEmailDeliveryRules.BuildPublicDownloadLink(opts, "abc123");
        Assert.Equal("https://api.regkasse.at/data/export-email/abc123", link);
    }
}
