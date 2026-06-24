using System.Text;
using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services.Billing;

public class InvoicePdfTemplateService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public InvoicePdfTemplateService(
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<string> GetTemplateHtmlAsync()
    {
        var templatePath = Path.Combine(
            _environment.ContentRootPath,
            "Templates",
            "InvoiceTemplate.html");

        if (!File.Exists(templatePath))
        {
            // Fallback to embedded template
            return GetDefaultTemplate();
        }

        return await File.ReadAllTextAsync(templatePath);
    }

    private string GetDefaultTemplate()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <style>
        body { font-family: Arial, sans-serif; margin: 40px; }
        .header { display: flex; justify-content: space-between; margin-bottom: 30px; }
        .logo { font-size: 24px; font-weight: bold; color: #1a56db; }
        .company-info { font-size: 12px; color: #666; }
        .invoice-title { font-size: 28px; font-weight: bold; margin: 20px 0; }
        .section { margin: 20px 0; }
        .address-block { background: #f8fafc; padding: 15px; border-radius: 8px; }
        .table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        .table th { background: #f1f5f9; text-align: left; padding: 10px; font-weight: 600; }
        .table td { padding: 10px; border-bottom: 1px solid #e2e8f0; }
        .table .total-row { font-weight: bold; background: #f8fafc; }
        .totals { margin-top: 20px; text-align: right; }
        .totals table { margin-left: auto; }
        .totals td { padding: 5px 15px; }
        .footer { margin-top: 40px; font-size: 11px; color: #64748b; border-top: 1px solid #e2e8f0; padding-top: 20px; }
        .disclaimer { background: #fef2f2; padding: 10px; border-radius: 4px; font-size: 11px; color: #991b1b; margin: 20px 0; }
        .payment-info { background: #f0fdf4; padding: 15px; border-radius: 8px; margin: 20px 0; }
    </style>
</head>
<body>
    <!-- Template content will be replaced by the service -->
    <div class='disclaimer'>⚠️ Dieses Dokument ist kein RKSV-Beleg.</div>
</body>
</html>";
    }
}
