namespace KasseAPI_Final.Services.Reports;

public static class JahresberichtPdfGenerator
{
    public static byte[] Generate(string documentNumber, string? registerLabel, PeriodSalesSummary sales) =>
        OperationalPeriodReportPdf.Generate("Jahresbericht", documentNumber, registerLabel, sales);
}
