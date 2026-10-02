using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Countries.QrRechnung;

/// <summary>
/// A6-height QR-bill (210 × 105 mm): receipt on the left, payment part on the right.
/// Swiss cross is painted into the module matrix. Not an RKSV fiscal receipt.
/// </summary>
internal static class QrRechnungPdf
{
    public static byte[] Render(QrRechnungPayload payload)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var modules = SwissCrossMatrix(payload.SwissQrText);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(210, 105, Unit.Millimetre);
                page.Margin(5, Unit.Millimetre);
                page.DefaultTextStyle(style => style.FontSize(8));
                page.Content().Row(row =>
                {
                    row.ConstantItem(62, Unit.Millimetre).Column(col =>
                    {
                        col.Item().Text("Empfangsschein").SemiBold();
                        WritePayableTo(col, payload);
                        WriteReference(col, payload);
                        WritePayableBy(col, payload);
                        WriteAmount(col, payload);
                        col.Item().PaddingTop(6).AlignRight().Text("Annahmestelle");
                    });
                    row.RelativeItem().PaddingLeft(4).Column(col =>
                    {
                        col.Item().Text("Zahlteil").SemiBold();
                        col.Item().Width(46, Unit.Millimetre).Height(46, Unit.Millimetre)
                            .Svg(SwissQrSvg(modules));
                        WriteAmount(col, payload);
                        WritePayableTo(col, payload);
                        WriteReference(col, payload);
                        WritePayableBy(col, payload);
                        if (!string.IsNullOrWhiteSpace(payload.AdditionalInfo))
                            col.Item().PaddingTop(2).Text(payload.AdditionalInfo);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void WritePayableTo(ColumnDescriptor col, QrRechnungPayload payload)
    {
        col.Item().PaddingTop(2).Text("Konto / Zahlbar an").SemiBold();
        col.Item().Text(payload.Iban);
        col.Item().Text(payload.Creditor.Name);
        if (!string.IsNullOrWhiteSpace(payload.Creditor.AddressLine1))
            col.Item().Text(payload.Creditor.AddressLine1);
        col.Item().Text($"{payload.Creditor.PostalCode} {payload.Creditor.City}".Trim());
        col.Item().Text(payload.Creditor.CountryCode);
    }

    private static void WriteReference(ColumnDescriptor col, QrRechnungPayload payload)
    {
        col.Item().PaddingTop(2).Text("Referenz").SemiBold();
        col.Item().Text(SwissQrEncoder.ToWire(payload.ReferenceType));
        if (!string.IsNullOrWhiteSpace(payload.Reference))
            col.Item().Text(payload.Reference);
    }

    private static void WritePayableBy(ColumnDescriptor col, QrRechnungPayload payload)
    {
        col.Item().PaddingTop(2).Text("Zahlbar durch");
        if (payload.Debtor is null)
            return;
        col.Item().Text(payload.Debtor.Name);
        if (!string.IsNullOrWhiteSpace(payload.Debtor.AddressLine1))
            col.Item().Text(payload.Debtor.AddressLine1);
        col.Item().Text($"{payload.Debtor.PostalCode} {payload.Debtor.City}".Trim());
    }

    private static void WriteAmount(ColumnDescriptor col, QrRechnungPayload payload)
    {
        col.Item().PaddingTop(2).Text("Währung").SemiBold();
        col.Item().Text(payload.Currency);
        col.Item().Text("Betrag").SemiBold();
        col.Item().Text(SwissQrEncoder.FormatAmount(payload.Amount));
    }

    internal static bool[,] SwissCrossMatrix(string swissQrText)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(swissQrText, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        var size = matrix.Count;
        var modules = new bool[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                modules[x, y] = matrix[y][x];
        }

        const int cross = 7;
        var start = (size - cross) / 2;
        var mid = start + (cross / 2);
        for (var y = start; y < start + cross; y++)
        {
            for (var x = start; x < start + cross; x++)
                modules[x, y] = true;
        }

        for (var i = start; i < start + cross; i++)
        {
            modules[i, mid] = false;
            modules[mid, i] = false;
        }

        return modules;
    }

    private static string SwissQrSvg(bool[,] modules)
    {
        var size = modules.GetLength(0);
        var sb = new System.Text.StringBuilder();
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ").Append(size).Append(' ').Append(size).Append("\">");
        sb.Append("<rect width=\"").Append(size).Append("\" height=\"").Append(size).Append("\" fill=\"white\"/>");
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (!modules[x, y])
                    continue;
                sb.Append("<rect x=\"").Append(x).Append("\" y=\"").Append(y).Append("\" width=\"1\" height=\"1\" fill=\"black\"/>");
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
