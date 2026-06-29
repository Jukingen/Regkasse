using KasseAPI_Final.Models;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KasseAPI_Final.Services.Billing;

public class InvoicePdfGenerator : IInvoicePdfGenerator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoicePdfGenerator> _logger;

    public InvoicePdfGenerator(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<InvoicePdfGenerator> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;

        // QuestPDF license (Community license for open source)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        var sale = await billingService.GetLicenseSaleAsync(saleId, ct).ConfigureAwait(false);

        var data = new InvoicePdfData
        {
            InvoiceNumber = sale.InvoiceNumber,
            TenantName = sale.TenantName,
            TenantSlug = sale.TenantSlug,
            TenantAddress = null,
            TenantVatId = null,
            TenantEmail = null,
            LicenseKey = sale.LicenseKey,
            LicensePlan = sale.LicensePlan,
            ValidFromUtc = sale.ValidFromUtc,
            ValidUntilUtc = sale.ValidUntilUtc,
            DurationDays = (sale.ValidUntilUtc - sale.ValidFromUtc).Days,
            PriceNet = sale.PriceNet,
            VatRate = sale.VatRate,
            VatAmount = sale.VatAmount,
            PriceGross = sale.PriceGross,
        };

        var pdf = GeneratePdf(data, isPreview: false);
        _logger.LogInformation("Generated license invoice PDF for sale {SaleId}", saleId);
        return pdf;
    }

    public Task<byte[]> GeneratePreviewPdfAsync(
        LicenseSalePreviewResponse preview,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var data = new InvoicePdfData
        {
            InvoiceNumber = preview.InvoiceNumber,
            TenantName = preview.TenantName,
            TenantSlug = preview.TenantSlug,
            TenantAddress = preview.TenantAddress,
            TenantVatId = preview.TenantVatId,
            TenantEmail = preview.TenantEmail,
            LicenseKey = preview.LicenseKey,
            LicensePlan = preview.LicensePlan,
            ValidFromUtc = preview.ValidFromUtc,
            ValidUntilUtc = preview.ValidUntilUtc,
            DurationDays = preview.DurationDays,
            PriceNet = preview.PriceNet,
            VatRate = preview.VatRate,
            VatAmount = preview.VatAmount,
            PriceGross = preview.PriceGross,
        };

        return Task.FromResult(GeneratePdf(data, isPreview: true));
    }

    public async Task<string> GetInvoicePdfBase64Async(
        Guid saleId,
        CancellationToken ct = default)
    {
        var pdfBytes = await GenerateInvoicePdfAsync(saleId, ct).ConfigureAwait(false);
        return Convert.ToBase64String(pdfBytes);
    }

    private byte[] GeneratePdf(InvoicePdfData data, bool isPreview)
    {
        var company = _configuration.GetSection("Company").Get<CompanyConfig>() ?? new CompanyConfig();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40, Unit.Point);
                page.DefaultTextStyle(x => x
                    .FontFamily("Arial", "Arial Unicode MS")
                    .FontSize(11));

                page.Header().Element(x => ComposeHeader(x, data, company, isPreview));
                page.Content().Element(x => ComposeContent(x, data, company));
                page.Footer().Element(x => ComposeFooter(x, company));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, InvoicePdfData data, CompanyConfig company, bool isPreview)
    {
        var companyName = SafeString(company.Name);
        var companyAddress = SafeString(company.Address);
        var companyVatId = SafeString(company.VatId);
        var invoiceNumber = SafeString(data.InvoiceNumber);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(companyName)
                        .FontSize(24)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    col.Item().Text(companyAddress)
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);

                    col.Item().Text($"UID: {companyVatId}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    if (isPreview)
                    {
                        col.Item().Text("VORSCHAU")
                            .FontSize(18)
                            .Bold()
                            .FontColor(Colors.Orange.Darken2);
                    }

                    col.Item().Text($"Rechnung: {invoiceNumber}")
                        .FontSize(12)
                        .Bold();

                    col.Item().Text($"Datum: {DateTime.Now:dd.MM.yyyy}")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken2);
        });
    }

    private static void ComposeContent(IContainer container, InvoicePdfData data, CompanyConfig company)
    {
        var tenantName = SafeString(data.TenantName, "Unbekannt");
        var tenantAddress = SafeString(data.TenantAddress, "Adresse nicht verfügbar");
        var tenantVatId = SafeString(data.TenantVatId, "nicht verfügbar");
        var tenantEmail = SafeString(data.TenantEmail, "nicht verfügbar");
        var invoiceNumber = SafeString(data.InvoiceNumber);
        var tenantSlug = SafeString(data.TenantSlug);
        var licenseKey = SafeString(data.LicenseKey);
        var planDisplay = SafeString(GetPlanDisplay(data.LicensePlan), "Unbekannt");
        var companyIban = SafeString(company.Bank?.Iban);
        var companyBic = SafeString(company.Bank?.Bic);

        container.Column(column =>
        {
            column.Spacing(10);

            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text("Leistungsempfänger")
                    .FontSize(10)
                    .Bold()
                    .FontColor(Colors.Grey.Darken1);

                col.Item().Text(text =>
                {
                    text.Span(tenantName).FontSize(12).Bold();
                });

                col.Item().Text(text =>
                {
                    text.Span(tenantAddress).FontSize(10);
                });

                col.Item().Text($"UID: {tenantVatId}")
                    .FontSize(10);

                col.Item().Text($"Email: {tenantEmail}")
                    .FontSize(10);
            });

            column.Item().Background(Colors.Red.Lighten5)
                .Border(1).BorderColor(Colors.Red.Lighten2)
                .Padding(8).Column(col =>
                {
                    col.Item().Text("⚠️ Dieses Dokument ist kein RKSV-Beleg und kein fiskalischer Beleg.")
                        .FontSize(10)
                        .FontColor(Colors.Red.Darken3);
                });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn(2);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Text("Pos.").Bold();
                    header.Cell().Text("Beschreibung").Bold();
                    header.Cell().Text("Netto").Bold().AlignRight();
                    header.Cell().Text("MwSt.").Bold().AlignCenter();
                    header.Cell().Text("Brutto").Bold().AlignRight();
                });

                table.Cell().Text("1");
                table.Cell().Column(col =>
                {
                    col.Item().Text($"{planDisplay} Lizenz").Bold();
                    col.Item().Text($"Lizenzschlüssel: {licenseKey}").FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(
                            $"Gültig: {data.ValidFromUtc:dd.MM.yyyy} – {data.ValidUntilUtc:dd.MM.yyyy} ({data.DurationDays} Tage)")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Text($"€ {data.PriceNet:F2}").AlignRight();
                table.Cell().Text($"{data.VatRate}%").AlignCenter();
                table.Cell().Text($"€ {data.PriceGross:F2}").AlignRight();
            });

            column.Item().AlignRight().Column(col =>
            {
                col.Spacing(5);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Netto:").AlignRight();
                    row.RelativeItem().Text($"€ {data.PriceNet:F2}").AlignRight();
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text($"MwSt. ({data.VatRate}%):").AlignRight();
                    row.RelativeItem().Text($"€ {data.VatAmount:F2}").AlignRight();
                });

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Gesamtbetrag:").Bold().AlignRight();
                    row.RelativeItem().Text($"€ {data.PriceGross:F2}").Bold().AlignRight();
                });
            });

            column.Item().Background(Colors.Green.Lighten5)
                .Border(1).BorderColor(Colors.Green.Lighten2)
                .Padding(10).Column(col =>
                {
                    col.Item().Text("Zahlungsinformationen").Bold();
                    col.Item().Text("Zahlungsbedingungen: Vorkasse, sofort fällig");
                    col.Item().Text($"IBAN: {companyIban}");
                    col.Item().Text($"BIC: {companyBic}");
                    col.Item().Text($"Verwendungszweck: {invoiceNumber} - {tenantSlug}");
                });
        });
    }

    private static void ComposeFooter(IContainer container, CompanyConfig company)
    {
        var companyName = SafeString(company.Name);
        var companyAddress = SafeString(company.Address);
        var companyVatId = SafeString(company.VatId);

        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text($"{companyName} · {companyAddress} · UID: {companyVatId}")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);

                row.RelativeItem().AlignRight().Text($"Generiert: {DateTime.Now:dd.MM.yyyy HH:mm}")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });

            column.Item().Text("Rechnungslegung gemäß §11 UStG. Es gelten unsere AGB.")
                .FontSize(8)
                .FontColor(Colors.Grey.Darken1)
                .AlignCenter();
        });
    }

    private static string GetPlanDisplay(string? plan) =>
        plan switch
        {
            LicenseSalePlans.SixMonths => "6 Monate",
            LicenseSalePlans.TwelveMonths => "1 Jahr",
            LicenseSalePlans.Custom => "Benutzerdefiniert",
            null or "" => "Unbekannt",
            _ => plan,
        };

    private static string SafeString(object? value, string defaultValue = "—") =>
        value?.ToString() ?? defaultValue;

    private sealed class InvoicePdfData
    {
        public required string InvoiceNumber { get; init; }
        public required string TenantName { get; init; }
        public required string TenantSlug { get; init; }
        public string? TenantAddress { get; init; }
        public string? TenantVatId { get; init; }
        public string? TenantEmail { get; init; }
        public required string LicenseKey { get; init; }
        public required string LicensePlan { get; init; }
        public DateTime ValidFromUtc { get; init; }
        public DateTime ValidUntilUtc { get; init; }
        public int DurationDays { get; init; }
        public decimal PriceNet { get; init; }
        public decimal VatRate { get; init; }
        public decimal VatAmount { get; init; }
        public decimal PriceGross { get; init; }
    }
}

public class CompanyConfig
{
    public string Name { get; set; } = "Regkasse Software";
    public string Address { get; set; } = "Hans Grüneis-Gasse 3, 2700 Wiener Neustadt";
    public string VatId { get; set; } = "ATU12345678";
    public string Phone { get; set; } = "+43 123 456 789";
    public string Email { get; set; } = "info@regkasse.at";
    public string Website { get; set; } = "www.regkasse.at";
    public BankConfig Bank { get; set; } = new();
}

public class BankConfig
{
    public string Iban { get; set; } = "AT00 0000 0000 0000 0000";
    public string Bic { get; set; } = "XXXAT2B";
}
