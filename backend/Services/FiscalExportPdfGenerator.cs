using System.Globalization;
using System.Linq;
using KasseAPI_Final.Models.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

/// <summary>
/// PDF summary for fiscal export: mandatory German legal footer on every page, first-page warning, watermark, optional internal signature block.
/// </summary>
public static class FiscalExportPdfGenerator
{
    private const int MaxReceiptRows = 500;

    /// <summary>Exact statutory footer text required on every PDF page (RKSV §8 context).</summary>
    public const string LegalFooterNoticeDe =
        "RECHTLICHER HINWEIS: Dieser Export ist kein rechtsverbindlicher Fiskalbeleg nach § 8 RKSV. "
        + "Nur für interne Analyse. Originalbeleg mit TSE-Signatur ist maßgeblich.";

    private const string WatermarkTextDe = "NICHT FÜR FINANZAMT - INTERNE ANALYSE";

    private static readonly Color FooterDarkRed = Color.FromHex("#8B0000");
    private static readonly Color FooterLightRedBg = Color.FromHex("#FFF0F0");
    private static readonly Color FooterTopRuleRed = Color.FromHex("#CC0000");
    private static readonly Color FirstPageWarningBg = Color.FromHex("#FFF9CC");
    private static readonly Color FirstPageWarningBorder = Color.FromHex("#8B0000");
    /// <summary>~30% opacity gray text (ARGB alpha ≈ 77/255).</summary>
    private static readonly Color WatermarkGrayTranslucent = Color.FromARGB(77, 72, 72, 72);

    static FiscalExportPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(FiscalExportPackageDto package, string disclaimerText)
    {
        var inv = CultureInfo.InvariantCulture;
        var receipts = package.Receipts.Take(MaxReceiptRows).ToList();
        var truncated = package.Receipts.Count > MaxReceiptRows;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.2f, Unit.Centimetre);
                page.MarginTop(0.55f, Unit.Centimetre);
                // Extra bottom space for multi-line 7pt footer + top rule.
                page.MarginBottom(2.0f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Background().Element(RenderDiagonalWatermark);

                page.Header().Column(header =>
                {
                    header.Item().ShowOnce().Element(c => RenderFirstPageWarningBox(c, disclaimerText));
                });

                page.Footer().Element(RenderLegalFooterEveryPage);

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Fiscal export (diagnostic / internal use only)").SemiBold().FontSize(14);
                    col.Item().Text(t =>
                    {
                        t.Span("Not a legally binding RKSV fiscal receipt (§ 8 RKSV). ").SemiBold();
                        t.Span("Consult the German legal notice in the page footer and the first-page warning box.");
                    });

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                        });

                        static void Row(TableDescriptor table, string label, string value)
                        {
                            table.Cell().Text(label).SemiBold();
                            table.Cell().Text(value);
                        }

                        Row(t, "Register", $"{package.RegisterNumber} ({package.CashRegisterId:D})");
                        Row(t, "Location", string.IsNullOrEmpty(package.RegisterLocation) ? "—" : package.RegisterLocation);
                        Row(t, "Period (UTC)", $"{package.Period.FromUtc:o} – {package.Period.ToUtc:o}");
                        Row(t, "Generated (UTC)", $"{package.GeneratedAtUtc:o}");
                        Row(t, "Export profile", package.ExportProfile);
                        Row(t, "Receipts in export", package.ReceiptCount.ToString(inv));
                        Row(t, "Closings in export", package.ClosingCount.ToString(inv));
                        if (truncated)
                            Row(t, "PDF rows", $"First {MaxReceiptRows} receipts shown; use JSON/CSV for full set.");
                    });

                    col.Item().PaddingTop(12).Text("Receipts (subset)").SemiBold().FontSize(11);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            static IContainer CellStyle(IContainer c) =>
                                c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).DefaultTextStyle(x => x.SemiBold());

                            h.Cell().Element(CellStyle).Text("Receipt #");
                            h.Cell().Element(CellStyle).Text("Issued (UTC)");
                            h.Cell().Element(CellStyle).AlignRight().Text("Total");
                            h.Cell().Element(CellStyle).Text("Signature (prefix)");
                        });

                        foreach (var r in receipts)
                        {
                            var sig = r.SignatureValue;
                            var sigShort = string.IsNullOrEmpty(sig) ? "—" : (sig.Length <= 24 ? sig : sig[..24] + "…");

                            table.Cell().PaddingVertical(3).Text(r.ReceiptNumber);
                            table.Cell().PaddingVertical(3).Text(r.IssuedAtUtc.ToString("o", inv));
                            table.Cell().PaddingVertical(3).AlignRight().Text(r.GrandTotal.ToString("F2", inv));
                            table.Cell().PaddingVertical(3).Text(sigShort);
                        }
                    });

                    col.Item().Element(RenderInternalApprovalSignatureSection);
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Diagonal watermark (30% opacity) behind all content, repeated on each page via <see cref="PageDescriptor.Background"/>.</summary>
    private static void RenderDiagonalWatermark(IContainer container)
    {
        container.Extend().AlignCenter().AlignMiddle().Element(layer =>
            layer.Rotate(-38)
                .Text(WatermarkTextDe)
                .FontSize(36)
                .SemiBold()
                .FontColor(WatermarkGrayTranslucent)
                .AlignCenter());
    }

    /// <summary>Yellow / red prominence box — first page header only (<see cref="ShowOnce"/>).</summary>
    private static void RenderFirstPageWarningBox(IContainer container, string disclaimerText)
    {
        container
            .PaddingBottom(8)
            .Border(2)
            .BorderColor(FirstPageWarningBorder)
            .Background(FirstPageWarningBg)
            .Padding(10)
            .Row(row =>
            {
                row.Spacing(10);
                row.ConstantItem(40).AlignMiddle().AlignCenter()
                    .Border(2)
                    .BorderColor(FirstPageWarningBorder)
                    .Background(Colors.White)
                    .Text("!")
                    .SemiBold()
                    .FontSize(22)
                    .FontColor(FirstPageWarningBorder)
                    .AlignCenter();

                row.RelativeItem().Column(body =>
                {
                    body.Spacing(4);
                    body.Item().Text("ACHTUNG — Kein amtlicher RKSV-/Finanzamt-Beleg")
                        .SemiBold()
                        .FontSize(10)
                        .FontColor(FooterDarkRed);

                    body.Item().Text(LegalFooterNoticeDe)
                        .FontSize(8)
                        .FontColor(FooterDarkRed)
                        .LineHeight(1.2f);

                    var extra = (disclaimerText ?? string.Empty).Trim();
                    if (extra.Length > 0 &&
                        !string.Equals(extra, LegalFooterNoticeDe, StringComparison.Ordinal))
                    {
                        body.Item().PaddingTop(4).Text(extra)
                            .FontSize(7.5f)
                            .Italic()
                            .FontColor(Color.FromHex("#553333"))
                            .LineHeight(1.15f);
                    }
                });
            });
    }

    /// <summary>Repeats on every printed page.</summary>
    private static void RenderLegalFooterEveryPage(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(FooterTopRuleRed);
            col.Item()
                .Background(FooterLightRedBg)
                .PaddingHorizontal(6)
                .PaddingVertical(6)
                .Text(LegalFooterNoticeDe)
                .FontSize(7)
                .Bold()
                .FontColor(FooterDarkRed)
                .AlignCenter()
                .LineHeight(1.2f);
        });
    }

    /// <summary>Optional internal acknowledgment lines at end of export body (appears after data; flows on natural last pages).</summary>
    private static void RenderInternalApprovalSignatureSection(IContainer container)
    {
        container.PaddingTop(28).Column(s =>
        {
            s.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            s.Item().PaddingTop(12).Text("Interne Freigabe / Verwendungsnachweis (optional)")
                .SemiBold()
                .FontSize(9)
                .FontColor(Color.FromRGB(51, 51, 51));
            s.Item().PaddingTop(14).Row(r =>
            {
                r.Spacing(16);
                r.RelativeItem().Text("Unterschrift (intern): _________________________________________").FontSize(9);
                r.RelativeItem().Text("Datum: _________________________________________").FontSize(9);
            });
        });
    }
}
