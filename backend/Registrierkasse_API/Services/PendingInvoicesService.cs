using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Registrierkasse_API.Services
{
    public interface IPendingInvoicesService
    {
        Task<List<Invoice>> GetPendingInvoicesAsync();
        Task<bool> SubmitPendingInvoicesAsync();
        Task<int> GetPendingCountAsync();
        Task<bool> RetryFailedSubmissionAsync(Guid invoiceId);
        Task<bool> MarkAsSubmittedAsync(Guid invoiceId);
        Task<bool> ClearOldPendingInvoicesAsync(int daysOld = 30);
    }

    public class PendingInvoicesService : IPendingInvoicesService
    {
        private readonly AppDbContext _context;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly INetworkConnectivityService _networkService;
        private readonly ILogger<PendingInvoicesService> _logger;

        public PendingInvoicesService(
            AppDbContext context,
            IFinanzOnlineService finanzOnlineService,
            INetworkConnectivityService networkService,
            ILogger<PendingInvoicesService> logger)
        {
            _context = context;
            _finanzOnlineService = finanzOnlineService;
            _networkService = networkService;
            _logger = logger;
        }

        public async Task<List<Invoice>> GetPendingInvoicesAsync()
        {
            try
            {
                return await _context.Invoices
                    .Where(i => !i.IsSubmittedToFinanzOnline && i.Status == InvoiceStatus.Sent)
                    .OrderBy(i => i.InvoiceDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen faturalar alınamadı");
                return new List<Invoice>();
            }
        }

        public async Task<bool> SubmitPendingInvoicesAsync()
        {
            try
            {
                // Network durumunu kontrol et
                var networkStatus = await _networkService.GetNetworkStatusAsync();
                if (!networkStatus.IsFinanzOnlineAvailable)
                {
                    _logger.LogWarning("FinanzOnline bağlantısı yok, bekleyen faturalar gönderilemedi");
                    return false;
                }

                var pendingInvoices = await GetPendingInvoicesAsync();
                if (!pendingInvoices.Any())
                {
                    _logger.LogInformation("Gönderilecek bekleyen fatura yok");
                    return true;
                }

                _logger.LogInformation("{Count} adet bekleyen fatura FinanzOnline'a gönderiliyor", pendingInvoices.Count);

                int successCount = 0;
                int failCount = 0;

                foreach (var invoice in pendingInvoices)
                {
                    try
                    {
                        var success = await SubmitToFinanzOnlineAsync(invoice);
                        if (success)
                        {
                            await MarkAsSubmittedAsync(invoice.Id);
                            successCount++;
                            _logger.LogInformation("Fatura başarıyla gönderildi: {InvoiceNumber}", invoice.InvoiceNumber);
                        }
                        else
                        {
                            failCount++;
                            _logger.LogWarning("Fatura gönderimi başarısız: {InvoiceNumber}", invoice.InvoiceNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        _logger.LogError(ex, "Fatura gönderimi hatası: {InvoiceNumber}", invoice.InvoiceNumber);
                    }
                }

                _logger.LogInformation("Bekleyen faturalar gönderimi tamamlandı. Başarılı: {Success}, Başarısız: {Fail}", 
                    successCount, failCount);

                return failCount == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen faturalar gönderimi genel hatası");
                return false;
            }
        }

        public async Task<int> GetPendingCountAsync()
        {
            try
            {
                return await _context.Invoices
                    .CountAsync(i => !i.IsSubmittedToFinanzOnline && i.Status == InvoiceStatus.Sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen fatura sayısı alınamadı");
                return 0;
            }
        }

        public async Task<bool> RetryFailedSubmissionAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(invoiceId);
                if (invoice == null)
                {
                    _logger.LogWarning("Fatura bulunamadı: {InvoiceId}", invoiceId);
                    return false;
                }

                if (invoice.IsSubmittedToFinanzOnline)
                {
                    _logger.LogInformation("Fatura zaten gönderilmiş: {InvoiceNumber}", invoice.InvoiceNumber);
                    return true;
                }

                var networkStatus = await _networkService.GetNetworkStatusAsync();
                if (!networkStatus.IsFinanzOnlineAvailable)
                {
                    _logger.LogWarning("FinanzOnline bağlantısı yok, yeniden deneme başarısız");
                    return false;
                }

                var success = await SubmitToFinanzOnlineAsync(invoice);
                if (success)
                {
                    await MarkAsSubmittedAsync(invoiceId);
                    _logger.LogInformation("Fatura yeniden gönderimi başarılı: {InvoiceNumber}", invoice.InvoiceNumber);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura yeniden gönderimi hatası: {InvoiceId}", invoiceId);
                return false;
            }
        }

        public async Task<bool> MarkAsSubmittedAsync(Guid invoiceId)
        {
            try
            {
                var invoice = await _context.Invoices.FindAsync(invoiceId);
                if (invoice == null)
                {
                    return false;
                }

                invoice.IsSubmittedToFinanzOnline = true;
                invoice.FinanzOnlineSubmissionDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura gönderildi olarak işaretleme hatası: {InvoiceId}", invoiceId);
                return false;
            }
        }

        public async Task<bool> ClearOldPendingInvoicesAsync(int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var oldInvoices = await _context.Invoices
                    .Where(i => i.InvoiceDate < cutoffDate && !i.IsSubmittedToFinanzOnline)
                    .ToListAsync();

                if (!oldInvoices.Any())
                {
                    return true;
                }

                _logger.LogInformation("{Count} adet eski bekleyen fatura temizleniyor", oldInvoices.Count);

                foreach (var invoice in oldInvoices)
                {
                    // Eski faturaları arşivle veya sil
                    invoice.Status = InvoiceStatus.Cancelled;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eski bekleyen faturalar temizleme hatası");
                return false;
            }
        }

        private async Task<bool> SubmitToFinanzOnlineAsync(Invoice invoice)
        {
            try
            {
                var finOnlineInvoice = new FinanzOnlineInvoice
                {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    TaxNumber = invoice.CompanyTaxNumber ?? "",
                    TseSignature = invoice.TseSignature,
                    CashRegisterId = invoice.KassenId,
                    Items = invoice.Items.ToList(),
                    TotalNet = invoice.Subtotal,
                    TotalTax = invoice.TaxAmount,
                    TotalGross = invoice.TotalAmount,
                    PaymentMethod = invoice.PaymentMethod?.ToString() ?? ""
                };

                return await _finanzOnlineService.SubmitInvoiceAsync(finOnlineInvoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline'a gönderim hatası: {InvoiceNumber}", invoice.InvoiceNumber);
                return false;
            }
        }
    }
} 