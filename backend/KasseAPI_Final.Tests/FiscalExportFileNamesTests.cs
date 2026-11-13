using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiscalExportFileNamesTests
{
    [Fact]
    public void Build_WithoutProfile_UsesRegisterPattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = FiscalExportFileNames.Build("cafe", "k1", profileName: null, "json", at);
        Assert.Equal("fiscal-export_cafe_k1_20260722_143022.json", name);
    }

    [Fact]
    public void Build_WithProfile_InsertsProfileSegment()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = FiscalExportFileNames.Build("cafe", "k1", "operational_preview", "json", at);
        Assert.Equal("fiscal-export_cafe_k1_operational_preview_20260722_143022.json", name);
    }

    [Fact]
    public void BuildWithProfile_OmitsRegister()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = FiscalExportFileNames.BuildWithProfile("cafe", "accounting_report", "json", at);
        Assert.Equal("fiscal-export_cafe_accounting_report_20260722_143022.json", name);
    }

    [Fact]
    public void Build_PdfExtension()
    {
        var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var name = FiscalExportFileNames.Build("cafe", "KASSE-01", "legal_compliance_export", "pdf", at);
        Assert.Equal("fiscal-export_cafe_KASSE-01_legal_compliance_export_20260102_030405.pdf", name);
    }
}
