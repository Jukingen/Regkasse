using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AuditExportFileNamesTests
{
    [Fact]
    public void Build_json_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        var name = AuditExportFileNames.Build(
            "cafe",
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 22),
            "json",
            at);
        Assert.Equal("audit_cafe_20260701_20260722_20260722_143022.json", name);
    }

    [Fact]
    public void Build_csv_matches_canonical_pattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        var name = AuditExportFileNames.Build(
            "cafe",
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 22),
            "csv",
            at);
        Assert.Equal("audit_cafe_20260701_20260722_20260722_143022.csv", name);
    }

    [Fact]
    public void Build_excel_uses_csv_extension_and_all_for_missing_dates()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22);
        Assert.Equal(
            "audit_cafe_all_all_20260722_143022.csv",
            AuditExportFileNames.Build("cafe", null, null, "excel", at));
    }
}
