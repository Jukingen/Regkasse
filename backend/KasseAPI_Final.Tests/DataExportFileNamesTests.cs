using KasseAPI_Final.Services.DataExport;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DataExportFileNamesTests
{
    [Fact]
    public void Build_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("data-export_cafe_20260722_143022.zip", DataExportFileNames.Build("cafe", at));
    }

    [Fact]
    public void Build_sanitizes_slug()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("data-export_Cafe_Alpha_20260722_143022.zip", DataExportFileNames.Build("Cafe Alpha", at));
    }
}
