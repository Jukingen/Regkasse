using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

namespace Registrierkasse_API.Services
{
    public interface IDailyReportService
    {
        Task<bool> ProcessDailyReportAsync(DateTime date);
        Task<bool> CheckDailyReportStatusAsync(DateTime date);
        Task<DailyReportResult> GetDailyReportAsync(DateTime date);
        Task<bool> SendDailyReportReminderAsync();
        Task<bool> IsDailyReportRequiredAsync(DateTime date);
    }

    public class DailyReportService : IDailyReportService
    {
        private readonly ILogger<DailyReportService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;

        public DailyReportService(
            ILogger<DailyReportService> logger,
            IServiceProvider serviceProvider,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
        }

        public async Task<bool> ProcessDailyReportAsync(DateTime date)
        {
            try
            {
                _logger.LogInformation("Günlük rapor işlemi başlatılıyor: {Date}", date.ToString("yyyy-MM-dd"));

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Günlük fişleri al
                var dailyInvoices = await GetDailyInvoicesAsync(context, date);
                if (!dailyInvoices.Any())
                {
                    _logger.LogWarning("Günlük fiş bulunamadı: {Date}", date.ToString("yyyy-MM-dd"));
                    return false;
                }

                // 2. TSE bağlantısını kontrol et
                if (!await _tseService.IsConnectedAsync())
                {
                    _logger.LogError("TSE cihazı bağlı değil, günlük rapor oluşturulamıyor");
                    return false;
                }

                // 3. Günlük rapor verilerini hesapla
                var reportData = CalculateDailyReportData(dailyInvoices, date);

                // 4. TSE ile günlük rapor imzala
                var tseSignature = await _tseService.SignDailyReportAsync();
                if (tseSignature == null)
                {
                    _logger.LogError("TSE günlük rapor imzalama başarısız");
                    return false;
                }

                // 5. Günlük rapor kaydını oluştur
                var dailyReport = new DailyReport
                {
                    Id = Guid.NewGuid(),
                    Date = date,
                    TseSignature = tseSignature.Signature,
                    TseSerialNumber = tseSignature.SerialNumber,
                    TseTime = tseSignature.Time,
                    TseProcessType = tseSignature.ProcessType,
                    TotalInvoices = reportData.TotalInvoices,
                    TotalAmount = reportData.TotalAmount,
                    TotalTaxAmount = reportData.TotalTaxAmount,
                    CashAmount = reportData.CashAmount,
                    CardAmount = reportData.CardAmount,
                    VoucherAmount = reportData.VoucherAmount,
                    StandardTaxAmount = reportData.StandardTaxAmount,
                    ReducedTaxAmount = reportData.ReducedTaxAmount,
                    SpecialTaxAmount = reportData.SpecialTaxAmount,
                    Status = DailyReportStatus.Completed.ToString(),
                    CreatedAt = DateTime.UtcNow
                };

                context.DailyReports.Add(dailyReport);
                await context.SaveChangesAsync();

                // 6. FinanzOnline'a gönder (opsiyonel)
                if (await _finanzOnlineService.AuthenticateAsync())
                {
                    try
                    {
                        var finanzOnlineReport = new FinanzOnlineDailyReport
                        {
                            Date = date,
                            TseSignature = tseSignature.Signature,
                            CashRegisterId = "DEMO-CASH-001",
                            InvoiceCount = reportData.TotalInvoices,
                            TotalAmount = reportData.TotalAmount,
                            CashAmount = reportData.CashAmount,
                            CardAmount = reportData.CardAmount,
                            VoucherAmount = reportData.VoucherAmount,
                            TaxStandard = reportData.StandardTaxAmount,
                            TaxReduced = reportData.ReducedTaxAmount,
                            TaxSpecial = reportData.SpecialTaxAmount
                        };

                        await _finanzOnlineService.SubmitDailyReportAsync(finanzOnlineReport);
                        _logger.LogInformation("Günlük rapor FinanzOnline'a gönderildi");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "FinanzOnline'a gönderim başarısız, rapor local'de saklandı");
                    }
                }

                _logger.LogInformation("Günlük rapor başarıyla tamamlandı: {Date}", date.ToString("yyyy-MM-dd"));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor işlemi başarısız: {Date}", date.ToString("yyyy-MM-dd"));
                return false;
            }
        }

        public async Task<bool> CheckDailyReportStatusAsync(DateTime date)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var existingReport = await context.DailyReports
                    .FirstOrDefaultAsync(r => r.Date.Date == date.Date);

                return existingReport != null && existingReport.Status == DailyReportStatus.Completed.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor durumu kontrol edilemedi: {Date}", date.ToString("yyyy-MM-dd"));
                return false;
            }
        }

        public async Task<DailyReportResult> GetDailyReportAsync(DateTime date)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var report = await context.DailyReports
                    .FirstOrDefaultAsync(r => r.Date.Date == date.Date);

                if (report == null)
                {
                    return new DailyReportResult { Exists = false };
                }

                return new DailyReportResult
                {
                    Exists = true,
                    Date = report.Date,
                    TseSignature = report.TseSignature,
                    TotalInvoices = report.TotalInvoices,
                    TotalAmount = report.TotalAmount,
                    TotalTaxAmount = report.TotalTaxAmount,
                    Status = report.Status.ToString()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor alınamadı: {Date}", date.ToString("yyyy-MM-dd"));
                return new DailyReportResult { Exists = false };
            }
        }

        public async Task<bool> SendDailyReportReminderAsync()
        {
            try
            {
                var today = DateTime.Today;
                var now = DateTime.Now;

                // 23:30'dan sonra ve günlük rapor henüz yapılmamışsa uyarı gönder
                if (now.Hour >= 23 && now.Minute >= 30)
                {
                    var isCompleted = await CheckDailyReportStatusAsync(today);
                    if (!isCompleted)
                    {
                        _logger.LogWarning("GÜNLÜK RAPOR UYARISI: Gün sonu raporu henüz yapılmadı!");
                        // Burada email, push notification veya başka bir uyarı sistemi eklenebilir
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor uyarısı gönderilemedi");
                return false;
            }
        }

        public async Task<bool> IsDailyReportRequiredAsync(DateTime date)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // O gün için fiş var mı kontrol et
                var hasInvoices = await context.Invoices
                    .AnyAsync(i => i.CreatedAt.Date == date.Date);

                if (!hasInvoices)
                {
                    return false; // Fiş yoksa rapor gerekmez
                }

                // Günlük rapor zaten yapılmış mı kontrol et
                var existingReport = await context.DailyReports
                    .FirstOrDefaultAsync(r => r.Date.Date == date.Date);

                return existingReport == null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor gerekliliği kontrol edilemedi: {Date}", date.ToString("yyyy-MM-dd"));
                return false;
            }
        }

        private async Task<List<Invoice>> GetDailyInvoicesAsync(AppDbContext context, DateTime date)
        {
            return await context.Invoices
                .Include(i => i.Items)
                .Where(i => i.CreatedAt.Date == date.Date)
                .ToListAsync();
        }

        private DailyReportCalculationData CalculateDailyReportData(List<Invoice> invoices, DateTime date)
        {
            var data = new DailyReportCalculationData
            {
                Date = date,
                TotalInvoices = invoices.Count,
                TotalAmount = invoices.Sum(i => i.TotalAmount),
                TotalTaxAmount = invoices.Sum(i => i.TaxAmount),
                CashAmount = invoices.Where(i => i.PaymentMethod == PaymentMethod.Cash).Sum(i => i.TotalAmount),
                CardAmount = invoices.Where(i => i.PaymentMethod == PaymentMethod.Card).Sum(i => i.TotalAmount),
                VoucherAmount = invoices.Where(i => i.PaymentMethod == PaymentMethod.Voucher).Sum(i => i.TotalAmount),
                StandardTaxAmount = 0,
                ReducedTaxAmount = 0,
                SpecialTaxAmount = 0
            };

            // Vergi detaylarını hesapla
            foreach (var invoice in invoices)
            {
                foreach (var item in invoice.Items)
                {
                    switch (item.TaxType)
                    {
                        case TaxType.Standard:
                            data.StandardTaxAmount += item.TaxAmount;
                            break;
                        case TaxType.Reduced:
                            data.ReducedTaxAmount += item.TaxAmount;
                            break;
                        case TaxType.Special:
                            data.SpecialTaxAmount += item.TaxAmount;
                            break;
                    }
                }
            }

            return data;
        }
    }

    public class DailyReportCalculationData
    {
        public DateTime Date { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal CashAmount { get; set; }
        public decimal CardAmount { get; set; }
        public decimal VoucherAmount { get; set; }
        public decimal StandardTaxAmount { get; set; }
        public decimal ReducedTaxAmount { get; set; }
        public decimal SpecialTaxAmount { get; set; }
    }

    public class DailyReportResult
    {
        public bool Exists { get; set; }
        public DateTime Date { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public enum DailyReportStatus
    {
        Pending,
        Completed,
        Failed
    }
} 
