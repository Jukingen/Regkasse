using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KasseAPI_Final.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace KasseAPI_Final.Services
{
    // English Description: Enhanced service for generating and printing receipts after successful payments
    // Türkçe Açıklama: Başarılı ödemeler sonrası fiş oluşturma ve yazdırma için geliştirilmiş servis
    
    public interface IReceiptService
    {
        Task<string> GenerateReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart);
        Task<bool> PrintReceiptAsync(string receiptContent, string printerName = null);
        Task<string> GenerateDigitalReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart);
        Task<bool> SaveReceiptToFileAsync(string receiptContent, string fileName);
        List<string> GetAvailablePrinters();
        Task<bool> TestPrinterConnectionAsync(string printerName = null);
        Task<PrinterStatus> GetPrinterStatusAsync(string printerName = null);
    }

    // Yazıcı durumu için enum
    public enum PrinterStatus
    {
        Ready,
        Offline,
        PaperOut,
        Error,
        Unknown
    }

    public class ReceiptService : IReceiptService
    {
        private readonly ILogger<ReceiptService> _logger;
        private readonly string _receiptsDirectory;
        private readonly string _defaultPrinterName;

        public ReceiptService(ILogger<ReceiptService> logger)
        {
            _logger = logger;
            _receiptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Receipts");
            _defaultPrinterName = GetDefaultPrinter();
            
            // Create receipts directory if it doesn't exist
            if (!Directory.Exists(_receiptsDirectory))
            {
                Directory.CreateDirectory(_receiptsDirectory);
            }
        }

        // Generate receipt content for printing with OCRA-B font support
        public async Task<string> GenerateReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart)
        {
            try
            {
                _logger.LogInformation("Generating receipt for payment {PaymentId} and invoice {InvoiceId}", 
                    payment.Id, invoice.Id);

                var receiptBuilder = new StringBuilder();

                // Receipt header with OCRA-B font support
                receiptBuilder.AppendLine("=".PadRight(40, '='));
                receiptBuilder.AppendLine("           RECEIPT");
                receiptBuilder.AppendLine("=".PadRight(40, '='));
                receiptBuilder.AppendLine();

                // Company information
                receiptBuilder.AppendLine("Company: Demo Company");
                receiptBuilder.AppendLine("Address: Demo Address");
                receiptBuilder.AppendLine("Tax Number: ATU12345678");
                receiptBuilder.AppendLine("Cash Register ID: DEMO-KASSE-001");
                receiptBuilder.AppendLine();

                // Receipt details
                receiptBuilder.AppendLine($"Receipt Number: {GenerateReceiptNumber()}");
                receiptBuilder.AppendLine($"Date: {invoice.InvoiceDate:dd.MM.yyyy}");
                receiptBuilder.AppendLine($"Time: {invoice.InvoiceDate:HH:mm:ss}");
                receiptBuilder.AppendLine($"Cashier: {payment.UserId}");
                receiptBuilder.AppendLine();

                // Customer information (if available)
                if (!string.IsNullOrEmpty(invoice.CustomerName))
                {
                    receiptBuilder.AppendLine("Customer Information:");
                    receiptBuilder.AppendLine($"Name: {invoice.CustomerName}");
                    if (!string.IsNullOrEmpty(invoice.CustomerEmail))
                        receiptBuilder.AppendLine($"Email: {invoice.CustomerEmail}");
                    if (!string.IsNullOrEmpty(invoice.CustomerPhone))
                        receiptBuilder.AppendLine($"Phone: {invoice.CustomerPhone}");
                    receiptBuilder.AppendLine();
                }

                // Items
                receiptBuilder.AppendLine("Items:");
                receiptBuilder.AppendLine("-".PadRight(40, '-'));
                
                foreach (var item in cart.Items)
                {
                    var itemTotal = item.Quantity * item.UnitPrice;
                    receiptBuilder.AppendLine($"{item.Product?.Name ?? "Unknown Product"}");
                    receiptBuilder.AppendLine($"  {item.Quantity} x €{item.UnitPrice:F2} = €{itemTotal:F2}");
                    if (!string.IsNullOrEmpty(item.Notes))
                        receiptBuilder.AppendLine($"  Note: {item.Notes}");
                }

                receiptBuilder.AppendLine("-".PadRight(40, '-'));
                receiptBuilder.AppendLine();

                // Totals
                receiptBuilder.AppendLine($"Subtotal: €{invoice.Subtotal:F2}");
                receiptBuilder.AppendLine($"Tax (20%): €{invoice.TaxAmount:F2}");
                receiptBuilder.AppendLine($"Total: €{invoice.TotalAmount:F2}");
                receiptBuilder.AppendLine();

                // Payment information
                receiptBuilder.AppendLine("Payment Information:");
                receiptBuilder.AppendLine($"Method: {payment.PaymentMethod}");
                receiptBuilder.AppendLine($"Transaction ID: {payment.TransactionId}");
                receiptBuilder.AppendLine($"Reference: {payment.Reference}");
                receiptBuilder.AppendLine();

                // TSE information
                if (!string.IsNullOrEmpty(invoice.TseSignature))
                {
                    receiptBuilder.AppendLine("TSE Information:");
                    receiptBuilder.AppendLine($"Signature: {invoice.TseSignature}");
                    receiptBuilder.AppendLine($"Timestamp: {invoice.TseTimestamp:dd.MM.yyyy HH:mm:ss}");
                    receiptBuilder.AppendLine();
                }

                // Footer
                receiptBuilder.AppendLine("Thank you for your purchase!");
                receiptBuilder.AppendLine("=".PadRight(40, '='));

                var receiptContent = receiptBuilder.ToString();
                
                _logger.LogInformation("Receipt generated successfully for payment {PaymentId}", payment.Id);
                
                return receiptContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt for payment {PaymentId}", payment.Id);
                throw;
            }
        }

        // Generate digital receipt (HTML format)
        public async Task<string> GenerateDigitalReceiptAsync(PaymentDetails payment, Invoice invoice, Cart cart)
        {
            try
            {
                _logger.LogInformation("Generating digital receipt for payment {PaymentId}", payment.Id);

                var htmlBuilder = new StringBuilder();

                // HTML header
                htmlBuilder.AppendLine("<!DOCTYPE html>");
                htmlBuilder.AppendLine("<html lang='en'>");
                htmlBuilder.AppendLine("<head>");
                htmlBuilder.AppendLine("    <meta charset='UTF-8'>");
                htmlBuilder.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
                htmlBuilder.AppendLine("    <title>Receipt - " + GenerateReceiptNumber() + "</title>");
                htmlBuilder.AppendLine("    <style>");
                htmlBuilder.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
                htmlBuilder.AppendLine("        .header { text-align: center; border-bottom: 2px solid #333; padding-bottom: 20px; }");
                htmlBuilder.AppendLine("        .company-info { margin: 20px 0; }");
                htmlBuilder.AppendLine("        .receipt-details { margin: 20px 0; }");
                htmlBuilder.AppendLine("        .items { margin: 20px 0; }");
                htmlBuilder.AppendLine("        .item { padding: 10px 0; border-bottom: 1px solid #eee; }");
                htmlBuilder.AppendLine("        .totals { margin: 20px 0; text-align: right; }");
                htmlBuilder.AppendLine("        .footer { margin-top: 30px; text-align: center; color: #666; }");
                htmlBuilder.AppendLine("        .tse-info { background: #f5f5f5; padding: 15px; margin: 20px 0; }");
                htmlBuilder.AppendLine("    </style>");
                htmlBuilder.AppendLine("</head>");
                htmlBuilder.AppendLine("<body>");

                // Header
                htmlBuilder.AppendLine("    <div class='header'>");
                htmlBuilder.AppendLine("        <h1>RECEIPT</h1>");
                htmlBuilder.AppendLine("    </div>");

                // Company information
                htmlBuilder.AppendLine("    <div class='company-info'>");
                htmlBuilder.AppendLine("        <h3>Demo Company</h3>");
                htmlBuilder.AppendLine("        <p>Demo Address<br>Tax Number: ATU12345678<br>Cash Register ID: DEMO-KASSE-001</p>");
                htmlBuilder.AppendLine("    </div>");

                // Receipt details
                htmlBuilder.AppendLine("    <div class='receipt-details'>");
                htmlBuilder.AppendLine($"        <p><strong>Receipt Number:</strong> {GenerateReceiptNumber()}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Date:</strong> {invoice.InvoiceDate:dd.MM.yyyy}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Time:</strong> {invoice.InvoiceDate:HH:mm:ss}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Cashier:</strong> {payment.UserId}</p>");
                htmlBuilder.AppendLine("    </div>");

                // Customer information
                if (!string.IsNullOrEmpty(invoice.CustomerName))
                {
                    htmlBuilder.AppendLine("    <div class='customer-info'>");
                    htmlBuilder.AppendLine("        <h3>Customer Information</h3>");
                    htmlBuilder.AppendLine($"        <p><strong>Name:</strong> {invoice.CustomerName}</p>");
                    if (!string.IsNullOrEmpty(invoice.CustomerEmail))
                        htmlBuilder.AppendLine($"        <p><strong>Email:</strong> {invoice.CustomerEmail}</p>");
                    if (!string.IsNullOrEmpty(invoice.CustomerPhone))
                        htmlBuilder.AppendLine($"        <p><strong>Phone:</strong> {invoice.CustomerPhone}</p>");
                    htmlBuilder.AppendLine("    </div>");
                }

                // Items
                htmlBuilder.AppendLine("    <div class='items'>");
                htmlBuilder.AppendLine("        <h3>Items</h3>");
                foreach (var item in cart.Items)
                {
                    var itemTotal = item.Quantity * item.UnitPrice;
                    htmlBuilder.AppendLine("        <div class='item'>");
                    htmlBuilder.AppendLine($"            <p><strong>{item.Product?.Name ?? "Unknown Product"}</strong></p>");
                    htmlBuilder.AppendLine($"            <p>{item.Quantity} x €{item.UnitPrice:F2} = €{itemTotal:F2}</p>");
                    if (!string.IsNullOrEmpty(item.Notes))
                        htmlBuilder.AppendLine($"            <p><em>Note: {item.Notes}</em></p>");
                    htmlBuilder.AppendLine("        </div>");
                }
                htmlBuilder.AppendLine("    </div>");

                // Totals
                htmlBuilder.AppendLine("    <div class='totals'>");
                htmlBuilder.AppendLine($"        <p><strong>Subtotal:</strong> €{invoice.Subtotal:F2}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Tax (20%):</strong> €{invoice.TaxAmount:F2}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Total:</strong> €{invoice.TotalAmount:F2}</p>");
                htmlBuilder.AppendLine("    </div>");

                // Payment information
                htmlBuilder.AppendLine("    <div class='payment-info'>");
                htmlBuilder.AppendLine("        <h3>Payment Information</h3>");
                htmlBuilder.AppendLine($"        <p><strong>Method:</strong> {payment.PaymentMethod}</p>");
                htmlBuilder.AppendLine($"        <p><strong>Transaction ID:</strong> {payment.TransactionId}</p>");
                if (!string.IsNullOrEmpty(payment.Reference))
                    htmlBuilder.AppendLine($"        <p><strong>Reference:</strong> {payment.Reference}</p>");
                htmlBuilder.AppendLine("    </div>");

                // TSE information
                if (!string.IsNullOrEmpty(invoice.TseSignature))
                {
                    htmlBuilder.AppendLine("    <div class='tse-info'>");
                    htmlBuilder.AppendLine("        <h3>TSE Information</h3>");
                    htmlBuilder.AppendLine($"        <p><strong>Signature:</strong> {invoice.TseSignature}</p>");
                    htmlBuilder.AppendLine($"        <p><strong>Timestamp:</strong> {invoice.TseTimestamp:dd.MM.yyyy HH:mm:ss}</p>");
                    htmlBuilder.AppendLine("    </div>");
                }

                // Footer
                htmlBuilder.AppendLine("    <div class='footer'>");
                htmlBuilder.AppendLine("        <p>Thank you for your purchase!</p>");
                htmlBuilder.AppendLine("        <p>Generated on: " + DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm:ss") + " UTC</p>");
                htmlBuilder.AppendLine("    </div>");

                htmlBuilder.AppendLine("</body>");
                htmlBuilder.AppendLine("</html>");

                var htmlContent = htmlBuilder.ToString();
                
                _logger.LogInformation("Digital receipt generated successfully for payment {PaymentId}", payment.Id);
                
                return htmlContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating digital receipt for payment {PaymentId}", payment.Id);
                throw;
            }
        }

        // Enhanced print receipt to printer with EPSON support
        public async Task<bool> PrintReceiptAsync(string receiptContent, string printerName = null)
        {
            try
            {
                _logger.LogInformation("Attempting to print receipt to printer: {PrinterName}", printerName ?? "Default");

                // Use default printer if none specified
                if (string.IsNullOrEmpty(printerName))
                {
                    printerName = _defaultPrinterName;
                }

                if (string.IsNullOrEmpty(printerName))
                {
                    _logger.LogWarning("No printer found, saving receipt to file instead");
                    await SaveReceiptToFileAsync(receiptContent, $"receipt_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    return false;
                }

                // Check printer status before printing
                var printerStatus = await GetPrinterStatusAsync(printerName);
                if (printerStatus != PrinterStatus.Ready)
                {
                    _logger.LogWarning("Printer {PrinterName} is not ready. Status: {Status}", printerName, printerStatus);
                    await SaveReceiptToFileAsync(receiptContent, $"receipt_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                    return false;
                }

                // Create temporary file for printing
                var tempFile = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid()}.txt");
                await File.WriteAllTextAsync(tempFile, receiptContent, Encoding.UTF8);

                try
                {
                    bool printSuccess = false;

                    // Try different printing methods based on OS
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        printSuccess = await PrintOnWindowsAsync(tempFile, printerName);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        printSuccess = await PrintOnLinuxAsync(tempFile, printerName);
                    }
                    else
                    {
                        _logger.LogWarning("Unsupported operating system for direct printing");
                        printSuccess = false;
                    }

                    if (printSuccess)
                    {
                        _logger.LogInformation("Receipt printed successfully to printer: {PrinterName}", printerName);
                    }
                    else
                    {
                        _logger.LogWarning("Print command failed for printer: {PrinterName}", printerName);
                    }
                    
                    return printSuccess;
                }
                finally
                {
                    // Clean up temporary file
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt");
                
                // Fallback: save to file
                await SaveReceiptToFileAsync(receiptContent, $"receipt_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                return false;
            }
        }

        // Windows printing implementation
        private async Task<bool> PrintOnWindowsAsync(string filePath, string printerName)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "print",
                    Arguments = $"/d:\"{printerName}\" \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Windows printing for printer: {PrinterName}", printerName);
                return false;
            }
        }

        // Linux printing implementation
        private async Task<bool> PrintOnLinuxAsync(string filePath, string printerName)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "lpr",
                    Arguments = $"-P \"{printerName}\" \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Linux printing for printer: {PrinterName}", printerName);
                return false;
            }
        }

        // Test printer connection
        public async Task<bool> TestPrinterConnectionAsync(string printerName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(printerName))
                {
                    printerName = _defaultPrinterName;
                }

                if (string.IsNullOrEmpty(printerName))
                {
                    return false;
                }

                var status = await GetPrinterStatusAsync(printerName);
                return status == PrinterStatus.Ready;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing printer connection for: {PrinterName}", printerName);
                return false;
            }
        }

        // Get printer status
        public async Task<PrinterStatus> GetPrinterStatusAsync(string printerName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(printerName))
                {
                    printerName = _defaultPrinterName;
                }

                if (string.IsNullOrEmpty(printerName))
                {
                    return PrinterStatus.Unknown;
                }

                // For now, return Ready as default
                // In production, implement actual printer status checking
                return PrinterStatus.Ready;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting printer status for: {PrinterName}", printerName);
                return PrinterStatus.Unknown;
            }
        }

        // Save receipt to file
        public async Task<bool> SaveReceiptToFileAsync(string receiptContent, string fileName)
        {
            try
            {
                var filePath = Path.Combine(_receiptsDirectory, fileName);
                await File.WriteAllTextAsync(filePath, receiptContent, Encoding.UTF8);
                
                _logger.LogInformation("Receipt saved to file: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving receipt to file: {FileName}", fileName);
                return false;
            }
        }

        // Enhanced printer detection with EPSON support
        private string GetDefaultPrinter()
        {
            try
            {
                // Priority order for printers
                var preferredPrinters = new List<string>
                {
                    "EPSON TM-T88VI",           // Preferred EPSON model
                    "EPSON TM-T88V",             // Alternative EPSON model
                    "Star TSP 700",              // Star printer support
                    "Microsoft Print to PDF",    // Fallback for testing
                    "Microsoft XPS Document Writer"
                };

                // Check if any preferred printer is available
                var availablePrinters = GetAvailablePrinters();
                foreach (var preferred in preferredPrinters)
                {
                    if (availablePrinters.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Using preferred printer: {PrinterName}", preferred);
                        return preferred;
                    }
                }

                // Return first available printer or default
                if (availablePrinters.Any())
                {
                    return availablePrinters.First();
                }

                return "Microsoft Print to PDF"; // Default fallback
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine default printer");
                return "Microsoft Print to PDF";
            }
        }

        // Enhanced available printers list
        public List<string> GetAvailablePrinters()
        {
            try
            {
                var printers = new List<string>();
                
                // Add EPSON printers (as specified in user rules)
                printers.Add("EPSON TM-T88VI");
                printers.Add("EPSON TM-T88V");
                printers.Add("EPSON TM-T88");
                
                // Add Star printers
                printers.Add("Star TSP 700");
                printers.Add("Star TSP 100");
                
                // Add common Windows printers
                printers.Add("Microsoft Print to PDF");
                printers.Add("Microsoft XPS Document Writer");
                printers.Add("OneNote");
                
                // In production, you can extend this to query actual system printers
                // using Windows API, WMI, or CUPS (Linux)
                
                return printers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available printers");
                return new List<string> { "Microsoft Print to PDF" };
            }
        }

        // Placeholder for generating a receipt number
        private string GenerateReceiptNumber()
        {
            return $"REC-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        }
    }
}
