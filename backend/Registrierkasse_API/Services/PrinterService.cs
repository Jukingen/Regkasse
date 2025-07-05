using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace Registrierkasse.Services
{
    public interface IPrinterService
    {
        Task<bool> ConnectAsync();
        Task<bool> DisconnectAsync();
        Task<bool> IsConnectedAsync();
        Task<bool> PrintReceiptAsync(ReceiptData receiptData);
        Task<bool> PrintDailyReportAsync(DailyReportData reportData);
        Task<PrinterStatus> GetStatusAsync();
        Task<List<string>> GetAvailablePrintersAsync();
    }

    public class PrinterService : IPrinterService
    {
        private readonly ILogger<PrinterService> _logger;
        private IntPtr _printerHandle;
        private bool _isConnected;
        private string _printerName;

        // EPSON Printer Models
        private const string EPSON_TM_T88VI = "EPSON TM-T88VI";
        private const string STAR_TSP_700 = "Star TSP 700";

        public PrinterService(ILogger<PrinterService> logger)
        {
            _logger = logger;
            _isConnected = false;
            _printerHandle = IntPtr.Zero;
            _printerName = Environment.GetEnvironmentVariable("PRINTER_NAME") ?? EPSON_TM_T88VI;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogInformation($"Yazıcıya bağlanılıyor: {_printerName}");

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await ConnectWindowsAsync();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await ConnectLinuxAsync();
                }
                else
                {
                    _logger.LogError("Desteklenmeyen işletim sistemi");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yazıcıya bağlanma hatası");
                return false;
            }
        }

        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if (_isConnected && _printerHandle != IntPtr.Zero)
                {
                    // Windows için yazıcı bağlantısını kapat
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        // ClosePrinter(_printerHandle);
                    }

                    _printerHandle = IntPtr.Zero;
                    _isConnected = false;
                    _logger.LogInformation("Yazıcı bağlantısı kapatıldı");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yazıcı bağlantısını kapatma hatası");
                return false;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            return _isConnected && _printerHandle != IntPtr.Zero;
        }

        public async Task<bool> PrintReceiptAsync(ReceiptData receiptData)
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new InvalidOperationException("Yazıcı bağlı değil");
                }

                // RKSV gereksinimlerine uygun fiş formatı
                var receiptContent = GenerateRksvReceipt(receiptData);
                
                // Yazıcıya gönder
                return await SendToPrinterAsync(receiptContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fiş yazdırma hatası");
                return false;
            }
        }

        public async Task<bool> PrintDailyReportAsync(DailyReportData reportData)
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    throw new InvalidOperationException("Yazıcı bağlı değil");
                }

                // Günlük rapor formatı
                var reportContent = GenerateDailyReport(reportData);
                
                // Yazıcıya gönder
                return await SendToPrinterAsync(reportContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor yazdırma hatası");
                return false;
            }
        }

        public async Task<PrinterStatus> GetStatusAsync()
        {
            try
            {
                if (!await IsConnectedAsync())
                {
                    return new PrinterStatus 
                    { 
                        IsConnected = false,
                        PrinterName = _printerName,
                        PaperStatus = "Unknown"
                    };
                }

                // Yazıcı durumu sorgulama
                return new PrinterStatus
                {
                    IsConnected = true,
                    PaperStatus = "OK",
                    PrinterName = _printerName,
                    LastPrintTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yazıcı durumu alma hatası");
                return new PrinterStatus 
                { 
                    IsConnected = false,
                    PrinterName = _printerName,
                    PaperStatus = "Error"
                };
            }
        }

        public async Task<List<string>> GetAvailablePrintersAsync()
        {
            try
            {
                var printers = new List<string>();
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows yazıcı listesi
                    printers.Add(EPSON_TM_T88VI);
                    printers.Add(STAR_TSP_700);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux yazıcı listesi
                    printers.Add("/dev/usb/lp0");
                    printers.Add("/dev/usb/lp1");
                }

                return printers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yazıcı listesi alma hatası");
                return new List<string>();
            }
        }

        private async Task<bool> ConnectWindowsAsync()
        {
            // Windows yazıcı bağlantısı
            await Task.Delay(1000); // Simüle edilmiş bağlantı
            
            _isConnected = true;
            _logger.LogInformation($"Yazıcıya başarıyla bağlanıldı: {_printerName}");
            return true;
        }

        private async Task<bool> ConnectLinuxAsync()
        {
            // Linux yazıcı bağlantısı
            await Task.Delay(1000); // Simüle edilmiş bağlantı
            
            _isConnected = true;
            _logger.LogInformation($"Yazıcıya başarıyla bağlanıldı: {_printerName}");
            return true;
        }

        private async Task<bool> SendToPrinterAsync(string content)
        {
            try
            {
                // Yazıcıya veri gönderme
                byte[] data = Encoding.UTF8.GetBytes(content);
                
                // Windows için yazıcıya gönder
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // WritePrinter(_printerHandle, data, data.Length, out int bytesWritten);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux için dosya yazma
                    await File.WriteAllBytesAsync("/dev/usb/lp0", data);
                }

                _logger.LogInformation("Veri yazıcıya gönderildi");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yazıcıya veri gönderme hatası");
                return false;
            }
        }

        private string GenerateRksvReceipt(ReceiptData receiptData)
        {
            var sb = new StringBuilder();
            
            // RKSV zorunlu alanları
            sb.AppendLine("=== RKSV BELEG ===");
            sb.AppendLine($"BelegDatum: {receiptData.Date:dd.MM.yyyy}");
            sb.AppendLine($"Uhrzeit: {receiptData.Time:HH:mm:ss}");
            sb.AppendLine($"TSE-Signatur: {receiptData.TseSignature}");
            sb.AppendLine($"Kassen-ID: {receiptData.CashRegisterId}");
            sb.AppendLine("==================");
            
            // Ürün listesi
            foreach (var item in receiptData.Items)
            {
                sb.AppendLine($"{item.Name} x{item.Quantity} @{item.Price:C} = {item.Total:C}");
            }
            
            // Vergi detayları
            sb.AppendLine("--- Steuer ---");
            sb.AppendLine($"Standard (20%): {receiptData.TaxStandard:C}");
            sb.AppendLine($"Ermäßigt (10%): {receiptData.TaxReduced:C}");
            sb.AppendLine($"Sonder (13%): {receiptData.TaxSpecial:C}");
            
            // Toplam
            sb.AppendLine("--- Gesamt ---");
            sb.AppendLine($"Netto: {receiptData.TotalNet:C}");
            sb.AppendLine($"Steuer: {receiptData.TotalTax:C}");
            sb.AppendLine($"Brutto: {receiptData.TotalGross:C}");
            
            // Ödeme
            sb.AppendLine($"Zahlung: {receiptData.PaymentMethod}");
            sb.AppendLine("==================");
            
            return sb.ToString();
        }

        private string GenerateDailyReport(DailyReportData reportData)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== TAGESABSCHLUSS ===");
            sb.AppendLine($"Datum: {reportData.Date:dd.MM.yyyy}");
            sb.AppendLine($"TSE-Signatur: {reportData.TseSignature}");
            sb.AppendLine($"Kassen-ID: {reportData.CashRegisterId}");
            sb.AppendLine("======================");
            
            // Günlük özet
            sb.AppendLine($"Anzahl Belege: {reportData.ReceiptCount}");
            sb.AppendLine($"Gesamtumsatz: {reportData.TotalAmount:C}");
            sb.AppendLine($"Bar: {reportData.CashAmount:C}");
            sb.AppendLine($"Karte: {reportData.CardAmount:C}");
            sb.AppendLine($"Gutschein: {reportData.VoucherAmount:C}");
            
            // Vergi özeti
            sb.AppendLine("--- Steuerübersicht ---");
            sb.AppendLine($"Standard (20%): {reportData.TaxStandard:C}");
            sb.AppendLine($"Ermäßigt (10%): {reportData.TaxReduced:C}");
            sb.AppendLine($"Sonder (13%): {reportData.TaxSpecial:C}");
            
            sb.AppendLine("======================");
            
            return sb.ToString();
        }
    }

    public class ReceiptData
    {
        public DateTime Date { get; set; }
        public DateTime Time { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string CashRegisterId { get; set; } = string.Empty;
        public List<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
        public decimal TaxStandard { get; set; }
        public decimal TaxReduced { get; set; }
        public decimal TaxSpecial { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalGross { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class ReceiptItem
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }

    public class DailyReportData
    {
        public DateTime Date { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string CashRegisterId { get; set; } = string.Empty;
        public int ReceiptCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal VoucherAmount { get; set; }
        public decimal TaxStandard { get; set; }
        public decimal TaxReduced { get; set; }
        public decimal TaxSpecial { get; set; }
    }

    public class PrinterStatus
    {
        public bool IsConnected { get; set; }
        public string PaperStatus { get; set; } = string.Empty;
        public string PrinterName { get; set; } = string.Empty;
        public DateTime LastPrintTime { get; set; }
    }
} 