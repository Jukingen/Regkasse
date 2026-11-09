namespace KasseAPI_Final.Services.Reports;

public static class TagesberichtPdfGenerator
{
    public static byte[] Generate(string documentNumber, string? registerLabel, PeriodSalesSummary sales) =>
        OperationalPeriodReportPdf.Generate("Tagesbericht", documentNumber, registerLabel, sales);
}
