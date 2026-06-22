using System.Globalization;
using KasseAPI_Final.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Billing;

/// <summary>Input for Mandanten SaaS billing invoice PDF (non-RKSV).</summary>
public sealed record LicenseSaleInvoiceDocument(
    LicenseSale Sale,
    string TenantName,
    string TenantSlug,
    string TenantAddress,
    string? TenantVatId,
    string? TenantEmail,
    CompanyProfileOptions Seller,
    byte[]? SellerLogoBytes = null);

public interface IInvoicePdfGenerator
{
    byte[] Generate(LicenseSaleInvoiceDocument document);
}

/// <summary>
/// QuestPDF generator for Super Admin license billing invoices.
/// Uses the same stack as <see cref="Services.InvoicePdfService"/> but without TSE/RKSV blocks.
/// </summary>
public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    public const string NonRksvDisclaimer =
        "Nicht RKSV-belegt — kein fiskalischer Beleg. Dieses Dokument dient ausschließlich der Abrechnung der Mandanten-SaaS-Lizenz.";

    static InvoicePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(LicenseSaleInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Sale);
        ArgumentNullException.ThrowIfNull(document.Seller);

        var sale = document.Sale;
        var seller = document.Seller;
        var culture = CultureInfo.GetCultureInfo("de-AT");

        var pdfDocument = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.ConstantItem(90).Column(logoCol =>
                        {
                            if (document.SellerLogoBytes is { Length: > 0 })
                            {
                                logoCol.Item().Height(48).Image(document.SellerLogoBytes).FitArea();
                            }
                            else
                            {
                                logoCol.Item().Height(48).AlignMiddle().Text(seller.CompanyName)
                                    .SemiBold()
                                    .FontSize(11);
                            }
                        });

                        row.RelativeItem().PaddingHorizontal(10).Column(col =>
                        {
                            col.Item().Text(seller.CompanyName).SemiBold().FontSize(16);
                            col.Item().Text(FormatSellerAddress(seller));
                            if (!string.IsNullOrWhiteSpace(seller.TaxNumber))
                                col.Item().Text($"UID: {seller.TaxNumber}");
                            if (!string.IsNullOrWhiteSpace(seller.Email))
                                col.Item().Text(seller.Email);
                            if (!string.IsNullOrWhiteSpace(seller.PhoneNumber))
                                col.Item().Text(seller.PhoneNumber);
                        });

                        row.ConstantItem(110).AlignRight().Text("RECHNUNG").FontSize(20).SemiBold();
                    });

                    header.Item().PaddingTop(8).Background(Colors.Grey.Lighten4).Padding(8).Text(NonRksvDisclaimer)
                        .FontSize(8)
                        .Italic()
                        .FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(0.75f, Unit.Centimetre).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Rechnungsempfänger:").SemiBold();
                            c.Item().Text(document.TenantName);
                            if (!string.IsNullOrWhiteSpace(document.TenantAddress))
                                c.Item().Text(document.TenantAddress);
                            if (!string.IsNullOrWhiteSpace(document.TenantVatId))
                                c.Item().Text($"UID: {document.TenantVatId}");
                            if (!string.IsNullOrWhiteSpace(document.TenantEmail))
                                c.Item().Text(document.TenantEmail);
                            c.Item().Text($"Mandant: {document.TenantSlug}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Rechnungsnr.: {sale.InvoiceNumber}");
                            c.Item().Text($"Datum: {sale.SoldAtUtc.ToString("dd.MM.yyyy", culture)}");
                            c.Item().Text($"Leistungszeitraum: {sale.ValidFromUtc:dd.MM.yyyy} – {sale.ValidUntilUtc:dd.MM.yyyy}");
                        });
                    });

                    col.Item().PaddingVertical(0.5f, Unit.Centimetre).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text("Leistung");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Netto");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text($"MwSt {sale.VatRate:0.##}%");
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text("Brutto");
                        });

                        table.Cell().Element(BodyCellStyle).Text(DescribePlan(sale));
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatMoney(sale.PriceNet, sale.Currency));
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatMoney(sale.VatAmount, sale.Currency));
                        table.Cell().Element(BodyCellStyle).AlignRight().Text(FormatMoney(sale.PriceGross, sale.Currency));
                    });

                    col.Item().PaddingTop(0.75f, Unit.Centimetre).Row(row =>
                    {
                        row.RelativeItem().Column(details =>
                        {
                            details.Item().Text("Lizenzdetails").SemiBold();
                            details.Item().Text($"Lizenzschlüssel: {sale.LicenseKey}");
                            details.Item().Text(
                                $"Gültig von {sale.ValidFromUtc:dd.MM.yyyy} bis {sale.ValidUntilUtc:dd.MM.yyyy} (UTC)");
                            if (!string.IsNullOrWhiteSpace(sale.Notes))
                                details.Item().Text($"Hinweis: {sale.Notes}");
                        });

                        row.ConstantItem(200).Column(vat =>
                        {
                            vat.Item().Text("MwSt-Aufschlüsselung").SemiBold();
                            vat.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Nettobetrag:");
                                r.ConstantItem(80).AlignRight().Text(FormatMoney(sale.PriceNet, sale.Currency));
                            });
                            vat.Item().Row(r =>
                            {
                                r.RelativeItem().Text($"Umsatzsteuer ({sale.VatRate:0.##}%):");
                                r.ConstantItem(80).AlignRight().Text(FormatMoney(sale.VatAmount, sale.Currency));
                            });
                            vat.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Gesamt brutto:").SemiBold();
                                r.ConstantItem(80).AlignRight().Text(FormatMoney(sale.PriceGross, sale.Currency)).SemiBold();
                            });
                        });
                    });

                    col.Item().PaddingTop(0.75f, Unit.Centimetre).Background(Colors.Grey.Lighten5).Padding(10).Column(payment =>
                    {
                        payment.Item().Text("Zahlungsinformationen").SemiBold();
                        payment.Item().Text($"Zahlungsart: Rechnung / Banküberweisung");
                        payment.Item().Text($"Währung: {sale.Currency}");
                        payment.Item().Text($"Verwendungszweck: {sale.InvoiceNumber}");
                        payment.Item().Text($"Rechnungsstatus: {DescribePaymentStatus(sale.Status)}");
                        if (!string.IsNullOrWhiteSpace(seller.Website))
                            payment.Item().Text($"Weitere Informationen: {seller.Website}");
                    });
                });

                page.Footer().AlignCenter().Column(footer =>
                {
                    footer.Item().Text(NonRksvDisclaimer).FontSize(7).FontColor(Colors.Grey.Darken1);
                    footer.Item().PaddingTop(4).DefaultTextStyle(x => x.FontSize(8)).Text(x =>
                    {
                        x.Span(seller.CompanyName);
                        x.Span(" · Seite ");
                        x.CurrentPageNumber();
                    });
                });
            });
        });

        return pdfDocument.GeneratePdf();
    }

    internal static byte[]? TryLoadLogoBytes(string? logoPathOrUrl, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(logoPathOrUrl))
            return null;

        var candidate = logoPathOrUrl.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsFile)
        {
            var localPath = absoluteUri.LocalPath;
            return File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;
        }

        if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            return File.ReadAllBytes(candidate);

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var httpUri)
            && (httpUri.Scheme == Uri.UriSchemeHttp || httpUri.Scheme == Uri.UriSchemeHttps))
        {
            return null;
        }

        var relative = Path.Combine(
            contentRootPath,
            candidate.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(relative) ? File.ReadAllBytes(relative) : null;
    }

    private static string DescribePlan(LicenseSale sale) =>
        sale.LicensePlan switch
        {
            LicenseSalePlans.SixMonths => "Mandantenlizenz 6 Monate",
            LicenseSalePlans.TwelveMonths => "Mandantenlizenz 12 Monate",
            LicenseSalePlans.Custom => "Mandantenlizenz (individuell)",
            _ => "Mandantenlizenz",
        };

    private static string DescribePaymentStatus(string status) =>
        status switch
        {
            LicenseSaleStatuses.Active => "Aktiv / abgerechnet",
            LicenseSaleStatuses.Cancelled => "Storniert",
            LicenseSaleStatuses.Refunded => "Erstattet",
            _ => status,
        };

    private static string FormatSellerAddress(CompanyProfileOptions seller) =>
        $"{seller.Street}, {seller.ZipCode} {seller.City}, {seller.Country}";

    private static string FormatMoney(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} {currency}");

    private static IContainer HeaderCellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5)
            .DefaultTextStyle(x => x.SemiBold());

    private static IContainer BodyCellStyle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(2);
}
