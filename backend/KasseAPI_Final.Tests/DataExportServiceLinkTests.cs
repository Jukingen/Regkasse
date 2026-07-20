using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.DataExport;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataExportServiceLinkTests
{
    [Fact]
    public void BuildDownloadLink_MatchesProductionSketchShape()
    {
        var opts = new DataExportOptions
        {
            PublicApiBaseUrl = "https://api.regkasse.at",
            DownloadPathTemplate = "/data/download/{token}",
        };

        var link = DataExportService.BuildDownloadLink(opts, "abc123");

        Assert.Equal("https://api.regkasse.at/data/download/abc123", link);
    }

    [Fact]
    public void BuildDownloadLink_TrimsBaseUrlSlash()
    {
        var opts = new DataExportOptions
        {
            PublicApiBaseUrl = "https://api.regkasse.at/",
            DownloadPathTemplate = "data/download/{token}",
        };

        var link = DataExportService.BuildDownloadLink(opts, "tok");

        Assert.Equal("https://api.regkasse.at/data/download/tok", link);
    }

    [Fact]
    public void ExportResult_SupportsLinkAndExpiry()
    {
        var expires = DateTime.UtcNow.AddDays(7);
        var result = new ExportResult
        {
            Link = "https://api.regkasse.at/data/download/x",
            ExpiresAt = expires,
            DownloadToken = "x",
            RequestId = Guid.NewGuid(),
        };

        Assert.NotNull(result.Link);
        Assert.Equal(expires, result.ExpiresAt);
    }
}
