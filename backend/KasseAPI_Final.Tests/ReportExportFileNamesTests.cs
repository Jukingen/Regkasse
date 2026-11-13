using KasseAPI_Final.Services.Reports;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReportExportFileNamesTests
{
    [Fact]
    public void Build_Tagesbericht()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var name = ReportExportFileNames.Build("tagesbericht", "cafe", "20260722", "pdf", at);
        Assert.Equal("report_tagesbericht_cafe_20260722_20260722_143022.pdf", name);
    }

    [Fact]
    public void Build_Monatsbericht_MapsMonatsbeleg()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var period = ReportExportFileNames.PeriodForReportType("monatsbeleg", new DateTime(2026, 7, 1));
        var name = ReportExportFileNames.Build("monatsbeleg", "cafe", period, "pdf", at);
        Assert.Equal("report_monatsbericht_cafe_202607_20260722_143022.pdf", name);
    }

    [Fact]
    public void Build_Jahresbericht_MapsJahresbeleg()
    {
        var at = new DateTime(2026, 7, 22, 14, 30, 22, DateTimeKind.Local);
        var period = ReportExportFileNames.PeriodForReportType("jahresbeleg", new DateTime(2026, 1, 1));
        var name = ReportExportFileNames.Build("jahresbeleg", "cafe", period, "pdf", at);
        Assert.Equal("report_jahresbericht_cafe_2026_20260722_143022.pdf", name);
    }

    [Fact]
    public void PeriodForReportType_DayMonthYear()
    {
        var day = new DateTime(2026, 7, 22);
        Assert.Equal("20260722", ReportExportFileNames.PeriodForReportType("tagesabschluss", day));
        Assert.Equal("202607", ReportExportFileNames.PeriodForReportType("monatsbericht", day));
        Assert.Equal("2026", ReportExportFileNames.PeriodForReportType("jahresbericht", day));
    }
}
