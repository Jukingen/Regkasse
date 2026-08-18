using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.License;

public sealed record LicenseCertificatePdfModel(
    string TenantName,
    string TenantSlug,
    string LicenseKeyDisplay,
    string Status,
    DateTime? ValidUntilUtc,
    DateTime GeneratedAtUtc);

/// <summary>Simple mandant license certificate PDF (non-fiscal).</summary>
public static class LicenseCertificatePdfGenerator
{
    static LicenseCertificatePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(LicenseCertificatePdfModel model)
    {
        var validUntil = model.ValidUntilUtc is { } until
            ? DateTime.SpecifyKind(until, DateTimeKind.Utc).ToString("dd.MM.yyyy HH:mm") + " UTC"
            : "—";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Header().Column(h =>
                {
                    h.Item().Text("Regkasse").Bold().FontSize(18);
                    h.Item().Text("Lizenzzertifikat / License certificate").FontSize(12).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingTop(24).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Dieses Dokument bestätigt den aktuellen Mandantenlizenzstatus.").FontSize(10);
                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);
                            c.RelativeColumn(2);
                        });
                        AddRow(table, "Mandant", model.TenantName);
                        AddRow(table, "Slug", model.TenantSlug);
                        AddRow(table, "Lizenzschlüssel", model.LicenseKeyDisplay);
                        AddRow(table, "Status", model.Status);
                        AddRow(table, "Gültig bis", validUntil);
                    });
                    col.Item().PaddingTop(16).Text(
                            $"Erstellt: {model.GeneratedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).Text("Kein Steuerbeleg. Keine RKSV-/TSE-Signatur.")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6)
            .Text(label).SemiBold();
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6)
            .Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
    }
}
