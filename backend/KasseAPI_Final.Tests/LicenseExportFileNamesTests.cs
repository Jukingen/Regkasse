using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseExportFileNamesTests
{
    [Fact]
    public void BuildSingle_matches_canonical_txt_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("license_cafe_20260722_143022.txt", LicenseExportFileNames.BuildSingle("cafe", at));
    }

    [Fact]
    public void BuildMultiple_json_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("licenses_cafe_20260722_143022.json", LicenseExportFileNames.BuildMultiple("cafe", "json", at));
    }

    [Fact]
    public void BuildMultiple_csv_uses_plural_prefix()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal("licenses_cafe_20260722_143022.csv", LicenseExportFileNames.BuildMultiple("cafe", "csv", at));
    }

    [Fact]
    public void NormalizeMultipleExtension_defaults_to_json()
    {
        Assert.Equal("json", LicenseExportFileNames.NormalizeMultipleExtension(null));
        Assert.Equal("json", LicenseExportFileNames.NormalizeMultipleExtension("xml"));
        Assert.Equal("csv", LicenseExportFileNames.NormalizeMultipleExtension("CSV"));
    }
}
