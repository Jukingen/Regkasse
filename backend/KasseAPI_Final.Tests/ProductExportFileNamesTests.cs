using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductExportFileNamesTests
{
    [Fact]
    public void Build_csv_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("product_cafe_20260722_143022.csv", ProductExportFileNames.Build("cafe", "csv", at));
    }

    [Fact]
    public void Build_json_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("product_cafe_20260722_143022.json", ProductExportFileNames.Build("cafe", "json", at));
    }

    [Fact]
    public void NormalizeExtension_defaults_to_csv()
    {
        Assert.Equal("csv", ProductExportFileNames.NormalizeExtension(null));
        Assert.Equal("csv", ProductExportFileNames.NormalizeExtension("excel"));
        Assert.Equal("json", ProductExportFileNames.NormalizeExtension("JSON"));
    }
}
