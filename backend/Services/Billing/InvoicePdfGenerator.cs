using KasseAPI_Final.Models;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace KasseAPI_Final.Services.Billing;

public class InvoicePdfGenerator : IInvoicePdfGenerator
{
    private static readonly SemaphoreSlim BrowserInitLock = new(1, 1);
    private static bool _browserDownloaded;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InvoicePdfTemplateService _templateService;
    private readonly ILogger<InvoicePdfGenerator> _logger;
    private readonly IConfiguration _configuration;

    public InvoicePdfGenerator(
        IServiceScopeFactory scopeFactory,
        InvoicePdfTemplateService templateService,
        ILogger<InvoicePdfGenerator> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _templateService = templateService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(
        Guid saleId,
        CancellationToken ct = default)
    {
        var sale = await GetLicenseSaleAsync(saleId, ct).ConfigureAwait(false);
        var template = await _templateService.GetTemplateHtmlAsync().ConfigureAwait(false);
        var html = BuildInvoiceHtml(sale, template);

        var pdf = await GeneratePdfFromHtmlAsync(html, ct).ConfigureAwait(false);
        _logger.LogInformation("Generated license invoice PDF for sale {SaleId}", saleId);
        return pdf;
    }

    public async Task<byte[]> GeneratePreviewPdfAsync(
        LicenseSalePreviewResponse preview,
        CancellationToken ct = default)
    {
        var template = await _templateService.GetTemplateHtmlAsync().ConfigureAwait(false);
        var html = BuildPreviewHtml(preview, template);

        return await GeneratePdfFromHtmlAsync(html, ct).ConfigureAwait(false);
    }

    public async Task<string> GetInvoicePdfBase64Async(
        Guid saleId,
        CancellationToken ct = default)
    {
        var pdfBytes = await GenerateInvoicePdfAsync(saleId, ct).ConfigureAwait(false);
        return Convert.ToBase64String(pdfBytes);
    }

    #region Private Methods

    private async Task<LicenseSaleResponse> GetLicenseSaleAsync(Guid saleId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();
        return await billingService.GetLicenseSaleAsync(saleId, ct).ConfigureAwait(false);
    }

    private string BuildInvoiceHtml(LicenseSaleResponse sale, string template)
    {
        var now = DateTime.UtcNow;

        var vatRate = sale.VatRate;
        var vatAmount = sale.VatAmount;
        var priceGross = sale.PriceGross;
        var priceNet = sale.PriceNet;

        var html = template
            .Replace("{{INVOICE_NUMBER}}", sale.InvoiceNumber)
            .Replace("{{INVOICE_DATE}}", now.ToString("dd. MMMM yyyy"))
            .Replace("{{TENANT_NAME}}", sale.TenantName)
            .Replace("{{TENANT_SLUG}}", sale.TenantSlug)
            .Replace("{{TENANT_ADDRESS}}", "Adresse nicht verfügbar") // TODO: Get from tenant
            .Replace("{{TENANT_VAT_ID}}", "UID nicht verfügbar") // TODO: Get from tenant
            .Replace("{{TENANT_EMAIL}}", "Email nicht verfügbar") // TODO: Get from tenant
            .Replace("{{LICENSE_KEY}}", sale.LicenseKey)
            .Replace("{{LICENSE_PLAN}}", GetPlanDisplay(sale.LicensePlan))
            .Replace("{{VALID_FROM}}", sale.ValidFromUtc.ToString("dd.MM.yyyy"))
            .Replace("{{VALID_UNTIL}}", sale.ValidUntilUtc.ToString("dd.MM.yyyy"))
            .Replace("{{DURATION_DAYS}}", ((sale.ValidUntilUtc - sale.ValidFromUtc).Days).ToString())
            .Replace("{{PRICE_NET}}", priceNet.ToString("F2"))
            .Replace("{{VAT_RATE}}", vatRate.ToString("F2"))
            .Replace("{{VAT_AMOUNT}}", vatAmount.ToString("F2"))
            .Replace("{{PRICE_GROSS}}", priceGross.ToString("F2"))
            .Replace("{{CURRENCY}}", sale.Currency)
            .Replace("{{COMPANY_NAME}}", _configuration["Company:Name"] ?? "Regkasse")
            .Replace("{{COMPANY_ADDRESS}}", _configuration["Company:Address"] ?? "Wiener Neustadt, Österreich")
            .Replace("{{COMPANY_VAT_ID}}", _configuration["Company:VatId"] ?? "ATU12345678")
            .Replace("{{COMPANY_PHONE}}", _configuration["Company:Phone"] ?? "+43 123 456 789")
            .Replace("{{COMPANY_EMAIL}}", _configuration["Company:Email"] ?? "info@regkasse.at")
            .Replace("{{COMPANY_WEBSITE}}", _configuration["Company:Website"] ?? "www.regkasse.at")
            .Replace("{{BANK_IBAN}}", _configuration["Company:Bank:Iban"] ?? "AT00 0000 0000 0000 0000")
            .Replace("{{BANK_BIC}}", _configuration["Company:Bank:Bic"] ?? "XXXAT2B")
            .Replace("{{GENERATED_AT}}", now.ToString("dd.MM.yyyy HH:mm"));

        return html;
    }

    private string BuildPreviewHtml(LicenseSalePreviewResponse preview, string template)
    {
        var now = DateTime.UtcNow;

        var html = template
            .Replace("{{INVOICE_NUMBER}}", "VORSCHAU - " + preview.InvoiceNumber)
            .Replace("{{INVOICE_DATE}}", now.ToString("dd. MMMM yyyy"))
            .Replace("{{TENANT_NAME}}", preview.TenantName)
            .Replace("{{TENANT_SLUG}}", preview.TenantSlug)
            .Replace("{{TENANT_ADDRESS}}", preview.TenantAddress ?? "Adresse nicht verfügbar")
            .Replace("{{TENANT_VAT_ID}}", preview.TenantVatId ?? "UID nicht verfügbar")
            .Replace("{{TENANT_EMAIL}}", preview.TenantEmail ?? "Email nicht verfügbar")
            .Replace("{{LICENSE_KEY}}", preview.LicenseKey)
            .Replace("{{LICENSE_PLAN}}", GetPlanDisplay(preview.LicensePlan))
            .Replace("{{VALID_FROM}}", preview.ValidFromUtc.ToString("dd.MM.yyyy"))
            .Replace("{{VALID_UNTIL}}", preview.ValidUntilUtc.ToString("dd.MM.yyyy"))
            .Replace("{{DURATION_DAYS}}", preview.DurationDays.ToString())
            .Replace("{{PRICE_NET}}", preview.PriceNet.ToString("F2"))
            .Replace("{{VAT_RATE}}", preview.VatRate.ToString("F2"))
            .Replace("{{VAT_AMOUNT}}", preview.VatAmount.ToString("F2"))
            .Replace("{{PRICE_GROSS}}", preview.PriceGross.ToString("F2"))
            .Replace("{{CURRENCY}}", preview.Currency)
            .Replace("{{COMPANY_NAME}}", _configuration["Company:Name"] ?? "Regkasse")
            .Replace("{{COMPANY_ADDRESS}}", _configuration["Company:Address"] ?? "Wiener Neustadt, Österreich")
            .Replace("{{COMPANY_VAT_ID}}", _configuration["Company:VatId"] ?? "ATU12345678")
            .Replace("{{COMPANY_PHONE}}", _configuration["Company:Phone"] ?? "+43 123 456 789")
            .Replace("{{COMPANY_EMAIL}}", _configuration["Company:Email"] ?? "info@regkasse.at")
            .Replace("{{COMPANY_WEBSITE}}", _configuration["Company:Website"] ?? "www.regkasse.at")
            .Replace("{{BANK_IBAN}}", _configuration["Company:Bank:Iban"] ?? "AT00 0000 0000 0000 0000")
            .Replace("{{BANK_BIC}}", _configuration["Company:Bank:Bic"] ?? "XXXAT2B")
            .Replace("{{GENERATED_AT}}", now.ToString("dd.MM.yyyy HH:mm"));

        html = html.Replace("</body>",
            "<div style='position: fixed; top: 50%; left: 50%; transform: rotate(-45deg); " +
            "font-size: 72px; opacity: 0.1; color: #666;'>VORSCHAU</div></body>");

        return html;
    }

    private async Task<byte[]> GeneratePdfFromHtmlAsync(string html, CancellationToken ct)
    {
        await EnsureBrowserDownloadedAsync(ct).ConfigureAwait(false);

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"],
        }).ConfigureAwait(false);

        await using var page = await browser.NewPageAsync().ConfigureAwait(false);
        await page.SetContentAsync(html).ConfigureAwait(false);

        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "20mm",
                Bottom = "20mm",
                Left = "15mm",
                Right = "15mm",
            },
        };

        ct.ThrowIfCancellationRequested();
        return await page.PdfDataAsync(pdfOptions).ConfigureAwait(false);
    }

    private static async Task EnsureBrowserDownloadedAsync(CancellationToken ct)
    {
        if (_browserDownloaded)
            return;

        await BrowserInitLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_browserDownloaded)
                return;

            var cachePath = Path.Combine(Path.GetTempPath(), "regkasse-puppeteer");
            var fetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Path = cachePath,
            });
            await fetcher.DownloadAsync().ConfigureAwait(false);
            _browserDownloaded = true;
        }
        finally
        {
            BrowserInitLock.Release();
        }
    }

    private static string GetPlanDisplay(string plan) =>
        plan switch
        {
            LicenseSalePlans.SixMonths => "6 Monate",
            LicenseSalePlans.TwelveMonths => "1 Jahr",
            LicenseSalePlans.Custom => "Benutzerdefiniert",
            _ => plan,
        };

    #endregion
}
