using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Reports;

/// <summary>Shared Umsatzübersicht block. Does not draw TSE signatures or QR codes.</summary>
internal static class PeriodSalesPdfSections
{
    public static void Write(ColumnDescriptor column, PeriodSalesSummary sales)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(sales);

        var deAt = CultureInfo.GetCultureInfo("de-AT");

        column.Item().PaddingTop(6).Text("Berichtszeitraum").Bold().FontSize(8);
        column.Item().Text(string.IsNullOrWhiteSpace(sales.PeriodLabel) ? "—" : sales.PeriodLabel).SemiBold();

        column.Item().PaddingTop(4).Text("Umsatzübersicht").Bold().FontSize(8);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(2);
            });

            void Row(string label, string value)
            {
                table.Cell().PaddingVertical(1).Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                table.Cell().PaddingVertical(1).AlignRight().Text(value).FontSize(8).SemiBold();
            }

            Row("Anzahl der Belege", sales.ReceiptCount.ToString(deAt));
            Row("Gesamtumsatz", Money(sales.TotalRevenue, deAt));
            Row("Netto", Money(sales.NetAmount, deAt));
            Row("MwSt. 10 %", Money(sales.Vat10, deAt));
            Row("MwSt. 20 %", Money(sales.Vat20, deAt));
            if (sales.Vat13 != 0m)
                Row("MwSt. 13 %", Money(sales.Vat13, deAt));
            if (sales.Vat49 != 0m)
                Row("MwSt. 4,9 %", Money(sales.Vat49, deAt));
            Row("Brutto", Money(sales.GrossAmount, deAt));

            table.Cell().ColumnSpan(2).PaddingTop(3).Text("Zahlungsarten").Bold().FontSize(8);
            foreach (var method in sales.PaymentMethods)
                Row(method.Label, Money(method.Amount, deAt));
        });

        if (sales.ZeroSales)
        {
            column.Item().PaddingTop(4)
                .Text(PeriodSalesSummary.ZeroSalesNote)
                .FontSize(8)
                .Italic();
        }
    }

    private static string Money(decimal amount, CultureInfo culture) =>
        $"{amount.ToString("N2", culture)} €";
}
