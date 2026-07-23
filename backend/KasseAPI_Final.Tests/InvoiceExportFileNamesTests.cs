using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class InvoiceExportFileNamesTests
{
    [Fact]
    public void BuildPdf_UsesCanonicalPattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = InvoiceExportFileNames.BuildPdf("cafe", "k1", "45", at);
        Assert.Equal("invoice_cafe_k1_45_20260722_143022.pdf", name);
    }

    [Fact]
    public void BuildList_Csv_UsesCanonicalPattern()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var from = new DateTime(2026, 7, 1);
        var to = new DateTime(2026, 7, 22);
        var name = InvoiceExportFileNames.BuildList("cafe", from, to, "csv", at);
        Assert.Equal("invoices_cafe_20260701_20260722_20260722_143022.csv", name);
    }

    [Fact]
    public void BuildList_ExcelExtension()
    {
        var at = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var name = InvoiceExportFileNames.BuildList("cafe", null, null, "xlsx", at);
        Assert.Equal("invoices_cafe_all_all_20260102_030405.xlsx", name);
    }
}
