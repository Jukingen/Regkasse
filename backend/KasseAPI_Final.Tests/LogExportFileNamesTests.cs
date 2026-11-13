using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LogExportFileNamesTests
{
    [Fact]
    public void Build_txt_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("log_cafe_20260722_143022.txt", LogExportFileNames.Build("cafe", "txt", at));
    }

    [Fact]
    public void Build_csv_and_json_use_same_prefix()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("log_cafe_20260722_143022.csv", LogExportFileNames.Build("cafe", "csv", at));
        Assert.Equal("log_cafe_20260722_143022.json", LogExportFileNames.Build("cafe", "json", at));
    }

    [Fact]
    public void NormalizeExtension_defaults_to_txt()
    {
        Assert.Equal("txt", LogExportFileNames.NormalizeExtension(null));
        Assert.Equal("txt", LogExportFileNames.NormalizeExtension("log"));
        Assert.Equal("csv", LogExportFileNames.NormalizeExtension("CSV"));
        Assert.Equal("json", LogExportFileNames.NormalizeExtension("JSON"));
    }
}
