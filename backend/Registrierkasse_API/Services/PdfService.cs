using Registrierkasse_API.Models;
using System.Text.Json;

namespace Registrierkasse_API.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, InvoiceTemplate? template = null);
        Task<byte[]> GenerateInvoicePdfAsync(string invoiceId);
        Task<byte[]> GenerateDailyReportPdfAsync(DailyReport report);
        Task<byte[]> GenerateReceiptPdfAsync(Receipt receipt);
    }

    public class PdfService : IPdfService
    {
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<PdfService> _logger;

        public PdfService(IInvoiceService invoiceService, ILogger<PdfService> logger)
        {
            _invoiceService = invoiceService;
            _logger = logger;
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, InvoiceTemplate? template = null)
        {
            try
            {
                _logger.LogInformation("Generating PDF for invoice {InvoiceNumber}", invoice.InvoiceNumber);
                
                // Basit HTML tabanlı PDF oluşturma (placeholder)
                var htmlContent = GenerateInvoiceHtml(invoice, template);
                var pdfBytes = ConvertHtmlToPdf(htmlContent);
                
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate invoice PDF for {InvoiceNumber}", invoice.InvoiceNumber);
                throw new NotImplementedException("PDF generation is not fully implemented yet");
            }
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceId)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId);
            return await GenerateInvoicePdfAsync(invoice);
        }

        public async Task<byte[]> GenerateDailyReportPdfAsync(DailyReport report)
        {
            try
            {
                _logger.LogInformation("Generating daily report PDF for {Date}", report.ReportDate);
                
                var htmlContent = GenerateDailyReportHtml(report);
                var pdfBytes = ConvertHtmlToPdf(htmlContent);
                
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate daily report PDF");
                throw new NotImplementedException("PDF generation is not fully implemented yet");
            }
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(Receipt receipt)
        {
            try
            {
                _logger.LogInformation("Generating receipt PDF for {ReceiptNumber}", receipt.ReceiptNumber);
                
                var htmlContent = GenerateReceiptHtml(receipt);
                var pdfBytes = ConvertHtmlToPdf(htmlContent);
                
                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate receipt PDF");
                throw new NotImplementedException("PDF generation is not fully implemented yet");
            }
        }

        private string GenerateInvoiceHtml(Invoice invoice, InvoiceTemplate? template)
        {
            var items = invoice.InvoiceItems != null 
                ? JsonSerializer.Deserialize<List<InvoiceItem>>(invoice.InvoiceItems.ToString()) 
                : new List<InvoiceItem>();

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Invoice #{invoice.InvoiceNumber}</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .footer {{ background-color: #f5f5f5; padding: 20px; text-align: center; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #007AFF; }}
                        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
                        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                        th {{ background-color: #f2f2f2; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Invoice #{invoice.InvoiceNumber}</h1>
                        <p>{invoice.CompanyName}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Bill To:</h2>
                        <p><strong>{invoice.CustomerName}</strong></p>
                        <p>{invoice.CustomerEmail}</p>
                        <p>{invoice.CustomerPhone}</p>
                        <p>{invoice.CustomerAddress}</p>
                        
                        <h3>Invoice Details:</h3>
                        <p><strong>Invoice Date:</strong> {invoice.InvoiceDate:dd.MM.yyyy}</p>
                        <p><strong>Due Date:</strong> {invoice.DueDate:dd.MM.yyyy}</p>
                        
                        <table>
                            <thead>
                                <tr>
                                    <th>Item</th>
                                    <th>Description</th>
                                    <th>Qty</th>
                                    <th>Unit Price</th>
                                    <th>Tax</th>
                                    <th>Total</th>
                                </tr>
                            </thead>
                            <tbody>
                                {string.Join("", items?.Select(item => $@"
                                    <tr>
                                        <td>{item.ProductName}</td>
                                        <td>{item.Description}</td>
                                        <td>{item.Quantity}</td>
                                        <td>€{item.UnitPrice:F2}</td>
                                        <td>€{item.TaxAmount:F2}</td>
                                        <td>€{item.TotalAmount:F2}</td>
                                    </tr>") ?? new string[0])}
                            </tbody>
                        </table>
                        
                        <div style='text-align: right;'>
                            <p><strong>Subtotal:</strong> €{invoice.Subtotal:F2}</p>
                            <p><strong>Tax:</strong> €{invoice.TaxAmount:F2}</p>
                            <p class='amount'>Total: €{invoice.TotalAmount:F2}</p>
                        </div>
                        
                        {(!string.IsNullOrEmpty(invoice.TseSignature) ? $@"
                        <h3>RKSV Information:</h3>
                        <p><strong>TSE Signature:</strong> {invoice.TseSignature}</p>
                        <p><strong>Kassen ID:</strong> {invoice.KassenId}</p>
                        <p><strong>TSE Timestamp:</strong> {invoice.TseTimestamp:dd.MM.yyyy HH:mm:ss}</p>
                        " : "")}
                    </div>
                    
                    <div class='footer'>
                        <p>{invoice.CompanyName}</p>
                        <p>{invoice.CompanyAddress}</p>
                        <p>Phone: {invoice.CompanyPhone}</p>
                        <p>Email: {invoice.CompanyEmail}</p>
                    </div>
                </body>
                </html>";
        }

        private string GenerateDailyReportHtml(DailyReport report)
        {
            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Daily Report - {report.ReportDate:dd.MM.yyyy}</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #007AFF; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>Daily Report</h1>
                        <p>{report.ReportDate:dd.MM.yyyy}</p>
                    </div>
                    
                    <div class='content'>
                        <h2>Summary</h2>
                        <p><strong>Total Sales:</strong> <span class='amount'>€{report.TotalSales:F2}</span></p>
                        <p><strong>Total Transactions:</strong> {report.TotalTransactions}</p>
                        <p><strong>Cash Payments:</strong> €{report.CashPayments:F2}</p>
                        <p><strong>Card Payments:</strong> €{report.CardPayments:F2}</p>
                        
                        {(!string.IsNullOrEmpty(report.TseSignature) ? $@"
                        <h3>TSE Information:</h3>
                        <p><strong>Kassen ID:</strong> {report.KassenId}</p>
                        <p><strong>Report Time:</strong> {report.ReportTime:dd.MM.yyyy HH:mm:ss}</p>
                        " : "")}
                    </div>
                </body>
                </html>";
        }

        private string GenerateReceiptHtml(Receipt receipt)
        {
            var items = receipt.Items != null 
                ? JsonSerializer.Deserialize<List<ReceiptItem>>(receipt.Items.ToString()) 
                : new List<ReceiptItem>();

            return $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <title>Receipt #{receipt.ReceiptNumber}</title>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 20px; }}
                        .header {{ background-color: #007AFF; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; }}
                        .amount {{ font-size: 18px; font-weight: bold; color: #007AFF; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <h1>RECEIPT</h1>
                        <p>Registrierkasse GmbH</p>
                    </div>
                    
                    <div class='content'>
                        <p><strong>Receipt Number:</strong> {receipt.ReceiptNumber}</p>
                        <p><strong>Date:</strong> {receipt.CreatedAt:dd.MM.yyyy HH:mm:ss}</p>
                        
                        <h3>Items:</h3>
                        {string.Join("", items?.Select(item => $@"
                            <p>{item.ProductName} x{item.Quantity} €{item.UnitPrice:F2} = €{item.TotalAmount:F2}</p>") ?? new string[0])}
                        
                        <p class='amount'>Total: €{receipt.TotalAmount:F2}</p>
                        
                        {(!string.IsNullOrEmpty(receipt.TseSignature) ? $@"
                        <h3>TSE Information:</h3>
                        <p><strong>TSE Signature:</strong> {receipt.TseSignature}</p>
                        <p><strong>Kassen ID:</strong> {receipt.KassenId}</p>
                        " : "")}
                        
                        <p style='text-align: center; margin-top: 30px;'>
                            <strong>Thank you for your purchase!</strong>
                        </p>
                    </div>
                </body>
                </html>";
        }

        private byte[] ConvertHtmlToPdf(string htmlContent)
        {
            // Basit HTML to PDF conversion (placeholder)
            // Gerçek uygulamada iTextSharp veya başka bir PDF kütüphanesi kullanılacak
            var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);
            
            // Şimdilik HTML içeriğini döndür (PDF olarak)
            return htmlBytes;
        }
    }

    public class ReceiptItem
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
    }
} 
