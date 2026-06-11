using System.Globalization;
using KasseAPI_Final.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services;

/// <summary>
/// Renders <see cref="RksvComplianceReportDto"/> to a German-disclaimered diagnostic PDF.
/// Mirrors the safety pattern used by <see cref="FiscalExportPdfGenerator"/> (watermark + per-page legal footer)
/// because this PDF must NEVER be presented as an official RKSV / Finanzamt proof.
/// </summary>
public static class RksvComplianceReportPdfGenerator
{
    /// <summary>Defensive cap on table rows per section to keep diagnostic PDFs readable.</summary>
    private const int MaxRowsPerSection = 250;

    /// <summary>Mandatory statutory-style footer wording (RKSV § 8 context, diagnostic-only).</summary>
    public const string LegalFooterNoticeDe =
        "RECHTLICHER HINWEIS: Dieser RKSV-Compliance-Bericht ist kein rechtsverbindlicher Beleg "
        + "nach § 8 RKSV. Nur für interne Compliance- und Diagnose-Zwecke. "
        + "Originalbeleg mit TSE-Signatur ist maßgeblich.";

    private const string WatermarkTextDe = "INTERNE COMPLIANCE-DIAGNOSE";

    private static readonly Color FooterDarkRed = Color.FromHex("#8B0000");
    private static readonly Color FooterLightRedBg = Color.FromHex("#FFF0F0");
    private static readonly Color FooterTopRuleRed = Color.FromHex("#CC0000");
    private static readonly Color FirstPageWarningBg = Color.FromHex("#FFF9CC");
    private static readonly Color FirstPageWarningBorder = Color.FromHex("#8B0000");
    private static readonly Color WatermarkGrayTranslucent = Color.FromARGB(64, 72, 72, 72);

    private static readonly Color SummaryPassBg = Color.FromHex("#E8F5E9");
    private static readonly Color SummaryFailBg = Color.FromHex("#FFEBEE");
    private static readonly Color SummaryNeutralBg = Color.FromHex("#F5F5F5");

    static RksvComplianceReportPdfGenerator()
    {
        // Defensive: FiscalExportPdfGenerator already sets this, but a static ctor here makes the
        // PDF builder usable without depending on initialization order of other generators.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(RksvComplianceReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var inv = CultureInfo.InvariantCulture;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.2f, Unit.Centimetre);
                page.MarginTop(0.55f, Unit.Centimetre);
                page.MarginBottom(2.0f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Background().Element(RenderDiagonalWatermark);

                page.Header().Column(header =>
                {
                    header.Item().ShowOnce().Element(c => RenderFirstPageWarningBox(c, report.LegalNoticeDe));
                });

                page.Footer().Element(RenderLegalFooterEveryPage);

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text("RKSV Compliance Test Report")
                        .SemiBold().FontSize(14);

                    col.Item().Text(t =>
                    {
                        t.Span("Diagnostic snapshot — ").SemiBold();
                        t.Span("not a legally binding RKSV fiscal document. ");
                        t.Span("See first-page warning box and per-page footer.");
                    });

                    RenderMetadataTable(col, report, inv);
                    RenderSummaryBox(col, report.Summary, inv);
                    RenderSpecialReceiptsSection(col, report.SpecialReceipts, inv);
                    RenderSignatureChainSection(col, report.SignatureChain, inv);
                    RenderSequenceGapsSection(col, report.SequenceGaps, inv);
                    RenderTseSignatureMissingSection(col, report.TseSignatureMissing, inv);
                    RenderQrValidationSection(col, report.QrPayloadValidation, inv);
                });
            });
        }).GeneratePdf();
    }

    private static void RenderMetadataTable(ColumnDescriptor col, RksvComplianceReportDto report, CultureInfo inv)
    {
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });

            static void Row(TableDescriptor table, string label, string value)
            {
                table.Cell().Text(label).SemiBold();
                table.Cell().Text(value);
            }

            Row(t, "Generated (UTC)", report.GeneratedAtUtc.ToString("o", inv));
            Row(t, "Cash register filter", report.CashRegisterId.HasValue
                ? report.CashRegisterId.Value.ToString("D", inv)
                : "All registers");
            Row(t, "From (UTC)", report.FromUtc?.ToString("o", inv) ?? "—");
            Row(t, "To (UTC)", report.ToUtc?.ToString("o", inv) ?? "—");
        });
    }

    private static void RenderSummaryBox(ColumnDescriptor col, RksvComplianceReportSummaryDto summary, CultureInfo inv)
    {
        var bg = summary.OverallPass ? SummaryPassBg : SummaryFailBg;

        col.Item().PaddingTop(4).Background(bg).Padding(8).Column(box =>
        {
            box.Item().Text(summary.OverallPass
                    ? "OVERALL: PASS — no chain breaks, sequence gaps, missing TSE signatures, or invalid QR formats detected."
                    : "OVERALL: FAIL — at least one compliance check produced findings.")
                .SemiBold()
                .FontSize(10);

            box.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c =>
                {
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                    c.RelativeColumn();
                });

                static void Cell(TableDescriptor table, string label, string value)
                {
                    table.Cell().Column(cell =>
                    {
                        cell.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
                        cell.Item().Text(value).SemiBold().FontSize(11);
                    });
                }

                Cell(t, "Registers", summary.RegistersCovered.ToString(inv));
                Cell(t, "Receipts scanned", summary.FiscalReceiptsScanned.ToString(inv));
                Cell(t, "Sonderbelege", summary.SpecialReceiptsCount.ToString(inv));
                Cell(t, "Chain breaks", summary.SignatureChainBreaks.ToString(inv));

                Cell(t, "Sequence gaps", summary.SequenceGapCount.ToString(inv));
                Cell(t, "TSE signature missing", summary.TseSignatureMissingCount.ToString(inv));
                Cell(t, "QR invalid format", summary.QrFormatInvalidCount.ToString(inv));
                Cell(t, "QR payload missing", summary.QrFormatMissingCount.ToString(inv));
            });
        });
    }

    private static void RenderSpecialReceiptsSection(
        ColumnDescriptor col,
        IReadOnlyList<RksvComplianceSpecialReceiptDto> rows,
        CultureInfo inv)
    {
        SectionHeader(col, "1) Special receipts (Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg / Nullbeleg)");
        if (rows.Count == 0)
        {
            col.Item().Text("No special receipts found in scope.").Italic();
            return;
        }

        var displayed = rows.Take(MaxRowsPerSection).ToList();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn();
                c.RelativeColumn();
            });

            HeaderRow(table, "Kind", "Register", "Receipt #", "Issued (UTC)", "Y/M", "TSE");

            foreach (var r in displayed)
            {
                BodyCell(table, r.Kind);
                BodyCell(table, r.RegisterNumber ?? r.CashRegisterId.ToString("D", inv));
                BodyCell(table, r.ReceiptNumber);
                BodyCell(table, r.IssuedAtUtc?.ToString("o", inv) ?? "—");
                BodyCell(table, $"{r.Year?.ToString(inv) ?? "—"}/{r.Month?.ToString(inv) ?? "—"}");
                BodyCell(table, r.HasTseSignature ? "OK" : "MISSING");
            }
        });

        TruncationHint(col, rows.Count, displayed.Count);
    }

    private static void RenderSignatureChainSection(
        ColumnDescriptor col,
        IReadOnlyList<RksvComplianceSignatureChainItemDto> rows,
        CultureInfo inv)
    {
        SectionHeader(col, "2) Signature chain continuity");

        // Compact: only show rows that aren't Pass.
        var issues = rows.Where(r => r.Status != RksvComplianceStatus.Pass).ToList();
        if (issues.Count == 0)
        {
            col.Item().Text("All inspected receipts chain correctly within the scan window.").Italic();
            return;
        }

        var displayed = issues.Take(MaxRowsPerSection).ToList();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });

            HeaderRow(table, "Status", "Register", "Receipt #", "Issued (UTC)", "Issue");

            foreach (var r in displayed)
            {
                BodyCell(table, r.Status);
                BodyCell(table, r.RegisterNumber ?? r.CashRegisterId.ToString("D", inv));
                BodyCell(table, r.ReceiptNumber);
                BodyCell(table, r.IssuedAtUtc.ToString("o", inv));
                BodyCell(table, r.Issue ?? string.Empty);
            }
        });

        TruncationHint(col, issues.Count, displayed.Count);
    }

    private static void RenderSequenceGapsSection(
        ColumnDescriptor col,
        IReadOnlyList<RksvComplianceSequenceGapDto> rows,
        CultureInfo inv)
    {
        SectionHeader(col, "3) Receipt number sequence gaps");
        if (rows.Count == 0)
        {
            col.Item().Text("No gaps detected in AT-{register}-{yyyyMMdd}-{seq} sequences.").Italic();
            return;
        }

        var displayed = rows.Take(MaxRowsPerSection).ToList();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });

            HeaderRow(table, "Register", "Day", "Missing #", "Previous", "Next");

            foreach (var r in displayed)
            {
                BodyCell(table, r.RegisterNumber ?? r.CashRegisterId.ToString("D", inv));
                BodyCell(table, r.SequenceDateUtc.ToString("yyyy-MM-dd", inv));
                BodyCell(table, r.ExpectedSequence.ToString(inv));
                BodyCell(table, r.PreviousReceiptNumber ?? "—");
                BodyCell(table, r.NextReceiptNumber ?? "—");
            }
        });

        TruncationHint(col, rows.Count, displayed.Count);
    }

    private static void RenderTseSignatureMissingSection(
        ColumnDescriptor col,
        IReadOnlyList<RksvComplianceTseSignatureMissingDto> rows,
        CultureInfo inv)
    {
        SectionHeader(col, "4) TSE signature presence (fiscal payments without a stored signature)");
        if (rows.Count == 0)
        {
            col.Item().Text("All fiscal receipts in scope carry a TSE signature.").Italic();
            return;
        }

        var displayed = rows.Take(MaxRowsPerSection).ToList();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn();
            });

            HeaderRow(table, "Register", "Receipt #", "Issued (UTC)", "Sonderbeleg", "Source");

            foreach (var r in displayed)
            {
                BodyCell(table, r.RegisterNumber ?? r.CashRegisterId.ToString("D", inv));
                BodyCell(table, r.ReceiptNumber);
                BodyCell(table, r.IssuedAtUtc?.ToString("o", inv) ?? "—");
                BodyCell(table, r.SpecialReceiptKind ?? "—");
                BodyCell(table, BuildMissingSourceLabel(r));
            }
        });

        TruncationHint(col, rows.Count, displayed.Count);
    }

    private static void RenderQrValidationSection(
        ColumnDescriptor col,
        IReadOnlyList<RksvComplianceQrValidationItemDto> rows,
        CultureInfo inv)
    {
        SectionHeader(col, "5) QR payload validation (RKSV BMF §9 / legacy _R1-AT1_ format)");
        if (rows.Count == 0)
        {
            col.Item().Text("All QR payloads in scope passed the format-only validation.").Italic();
            return;
        }

        var displayed = rows.Take(MaxRowsPerSection).ToList();
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });

            HeaderRow(table, "Register", "Receipt #", "Issued (UTC)", "Issue / Errors");

            foreach (var r in displayed)
            {
                BodyCell(table, r.RegisterNumber ?? r.CashRegisterId.ToString("D", inv));
                BodyCell(table, r.ReceiptNumber);
                BodyCell(table, r.IssuedAtUtc.ToString("o", inv));
                var summary = r.QrPayloadMissing
                    ? "QR payload missing"
                    : (r.IsValidFormat ? "OK" : string.Join("; ", r.Errors));
                BodyCell(table, summary);
            }
        });

        TruncationHint(col, rows.Count, displayed.Count);
    }

    private static string BuildMissingSourceLabel(RksvComplianceTseSignatureMissingDto r)
    {
        if (r.PaymentSignatureMissing && r.ReceiptSignatureMissing)
            return "payment + receipt";
        if (r.PaymentSignatureMissing)
            return "payment";
        return "receipt";
    }

    private static void SectionHeader(ColumnDescriptor col, string text)
    {
        col.Item().PaddingTop(8).Text(text).SemiBold().FontSize(11);
    }

    private static void TruncationHint(ColumnDescriptor col, int total, int shown)
    {
        if (total > shown)
        {
            col.Item().Text($"… {total - shown} more rows truncated. Use the JSON response for the full set.")
                .Italic().FontSize(8).FontColor(Colors.Grey.Darken1);
        }
    }

    private static void HeaderRow(TableDescriptor table, params string[] headers)
    {
        table.Header(h =>
        {
            static IContainer CellStyle(IContainer c) =>
                c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
                    .DefaultTextStyle(x => x.SemiBold());

            foreach (var label in headers)
                h.Cell().Element(CellStyle).Text(label);
        });
    }

    private static void BodyCell(TableDescriptor table, string value)
    {
        table.Cell().PaddingVertical(3).Text(value ?? string.Empty).FontSize(8.5f);
    }

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
}
