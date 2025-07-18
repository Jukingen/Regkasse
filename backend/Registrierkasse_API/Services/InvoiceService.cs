using Registrierkasse_API.Data;
using Registrierkasse_API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Registrierkasse_API.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceCreateResponse> CreateInvoiceAsync(InvoiceCreateRequest request);
        Task<Invoice> GetInvoiceByIdAsync(string id);
        Task<Invoice> GetInvoiceByNumberAsync(string invoiceNumber);
        Task<List<Invoice>> GetInvoicesAsync(InvoiceFilterRequest filter);
        Task<Invoice> UpdateInvoiceAsync(string id, InvoiceUpdateRequest request);
        Task DeleteInvoiceAsync(string id);
        Task<Invoice> SendInvoiceAsync(string id, InvoiceSendRequest request);
        Task<Invoice> MarkAsPaidAsync(string id, InvoicePaymentRequest request);
        Task<Invoice> CancelInvoiceAsync(string id, string reason);
        Task<bool> SubmitToFinanzOnlineAsync(string id);
        Task<List<Invoice>> GetOverdueInvoicesAsync();
        Task<InvoiceStatistics> GetInvoiceStatisticsAsync(DateTime startDate, DateTime endDate);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _context;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly INetworkConnectivityService _networkService;
        private readonly IPendingInvoicesService _pendingInvoicesService;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            AppDbContext context,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            INetworkConnectivityService networkService,
            IPendingInvoicesService pendingInvoicesService,
            ILogger<InvoiceService> logger)
        {
            _context = context;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _networkService = networkService;
            _pendingInvoicesService = pendingInvoicesService;
            _logger = logger;
        }

        public async Task<InvoiceCreateResponse> CreateInvoiceAsync(InvoiceCreateRequest request)
        {
            try
            {
                // TSE bağlantısını kontrol et (zorunlu - RKSV §6)
                var tseStatus = await _tseService.GetStatusAsync();
                if (!tseStatus.IsConnected)
                {
                    var errorMessage = "TSE cihazı bağlı değil. RKSV standartlarına göre fiş kesilemez.";
                    _logger.LogError("TSE cihazı bağlı değil. Fatura oluşturulamadı.");
                    throw new InvalidOperationException(errorMessage);
                }

                // TSE sertifikasını kontrol et
                if (tseStatus.CertificateStatus != "VALID")
                {
                    var errorMessage = $"TSE sertifikası geçersiz: {tseStatus.CertificateStatus}. Fiş kesilemez.";
                    _logger.LogError("TSE sertifikası geçersiz. Fatura oluşturulamadı.");
                    throw new InvalidOperationException(errorMessage);
                }

                // Network durumunu kontrol et
                var networkStatus = await _networkService.GetNetworkStatusAsync();
                
                _logger.LogInformation("Fatura oluşturuluyor. TSE: {TseConnected}, Network: {Status}", 
                    tseStatus.IsConnected, networkStatus.Status);

                // Bağlantı durumunu kontrol et ve kullanıcıyı bilgilendir
                if (!networkStatus.IsInternetAvailable)
                {
                    _logger.LogWarning("İnternet bağlantısı yok. Fatura local'de oluşturulacak.");
                    // Kullanıcıya uyarı verilebilir ama fatura kesilmeye devam eder
                }

                var invoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = GenerateInvoiceNumber(),
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    CustomerAddress = request.CustomerAddress,
                    CustomerTaxNumber = request.CustomerTaxNumber,
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = request.DueDate ?? DateTime.UtcNow.AddDays(30),
                    Subtotal = request.Subtotal,
                    TaxAmount = request.TaxAmount,
                    TotalAmount = request.TotalAmount,
                    PaidAmount = 0,
                    RemainingAmount = request.TotalAmount,
                    Status = InvoiceStatus.Draft,
                    PaymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod, true),
                    PaymentReference = request.PaymentReference,
                    CompanyName = request.CompanyName,
                    CompanyAddress = request.CompanyAddress,
                    CompanyPhone = request.CompanyPhone,
                    CompanyEmail = request.CompanyEmail,
                    CompanyTaxNumber = request.CompanyTaxNumber,
                    TermsAndConditions = request.TermsAndConditions,
                    Notes = request.Notes,
                    InvoiceItems = JsonSerializer.SerializeToDocument(request.InvoiceItems),
                    CreatedById = request.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CustomerId = request.CustomerId
                };

                // TSE imzası oluştur (zorunlu)
                try
                {
                    var processData = $"INVOICE_{invoice.InvoiceNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    var tseSignature = await _tseService.SignTransactionAsync(processData, "INVOICE");
                    
                    invoice.TseSignature = tseSignature.Signature;
                    invoice.TseTimestamp = tseSignature.Time;
                    invoice.KassenId = await _tseService.GetTseIdAsync();
                    
                    _logger.LogInformation("TSE imzası başarıyla oluşturuldu: {Signature}", tseSignature.Signature);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TSE imzası oluşturulamadı");
                    throw new InvalidOperationException("TSE imzası oluşturulamadı. Fiş kesilemez.");
                }

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice created: {InvoiceNumber}", invoice.InvoiceNumber);

                // FinanzOnline'a gönderim (sadece bağlantı varsa)
                bool isSubmittedToFinanzOnline = false;
                if (networkStatus.IsFinanzOnlineAvailable)
                {
                    try
                    {
                        await SubmitToFinanzOnlineAsync(invoice.Id.ToString());
                        _logger.LogInformation("Fatura FinanzOnline'a başarıyla gönderildi: {InvoiceNumber}", invoice.InvoiceNumber);
                        isSubmittedToFinanzOnline = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "FinanzOnline'a gönderim başarısız, fatura local'de saklandı. Invoice: {InvoiceNumber}", invoice.InvoiceNumber);
                        // Fatura local'de saklanır, bağlantı geldiğinde gönderilir
                    }
                }
                else
                {
                    _logger.LogInformation("FinanzOnline bağlantısı yok, fatura local'de saklandı. Invoice ID: {InvoiceId}, Number: {InvoiceNumber}", 
                        invoice.Id, invoice.InvoiceNumber);
                    
                    // Kullanıcıya bilgi ver: Fatura oluşturuldu ama FinanzOnline'a gönderilemedi
                    // Bu bilgi response'da döndürülebilir
                }

                // Pending invoices bilgisini al
                var pendingCount = await _pendingInvoicesService.GetPendingCountAsync();

                return new InvoiceCreateResponse
                {
                    Invoice = invoice,
                    IsSubmittedToFinanzOnline = isSubmittedToFinanzOnline,
                    NetworkStatus = networkStatus.Status,
                    TseStatus = tseStatus.IsConnected ? "CONNECTED" : "DISCONNECTED",
                    WarningMessage = !networkStatus.IsInternetAvailable ? "İnternet bağlantısı yok. Fatura local'de oluşturuldu." : null,
                    HasPendingInvoices = pendingCount > 0,
                    PendingCount = pendingCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatura oluşturma hatası");
                throw;
            }
        }

        public async Task<Invoice> GetInvoiceByIdAsync(string id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
                throw new ArgumentException("Invoice not found");

            return invoice;
        }

        public async Task<Invoice> GetInvoiceByNumberAsync(string invoiceNumber)
        {
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
            
            if (invoice == null)
                throw new ArgumentException("Invoice not found");

            return invoice;
        }

        public async Task<List<Invoice>> GetInvoicesAsync(InvoiceFilterRequest filter)
        {
            var query = _context.Invoices.AsQueryable();

            if (!string.IsNullOrEmpty(filter.Status))
            {
                if (Enum.TryParse<InvoiceStatus>(filter.Status, out var status))
                {
                    query = query.Where(i => i.Status == status);
                }
            }

            if (!string.IsNullOrEmpty(filter.CustomerId))
            {
                query = query.Where(i => i.CustomerId == filter.CustomerId);
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate <= filter.EndDate.Value);
            }

            if (filter.MinAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount >= filter.MinAmount.Value);
            }

            if (filter.MaxAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount <= filter.MaxAmount.Value);
            }

            // Sorting
            if (!string.IsNullOrEmpty(filter.SortBy))
            {
                query = filter.SortBy.ToLower() switch
                {
                    "invoicedate" => filter.SortDescending == true 
                        ? query.OrderByDescending(i => i.InvoiceDate)
                        : query.OrderBy(i => i.InvoiceDate),
                    "totalamount" => filter.SortDescending == true 
                        ? query.OrderByDescending(i => i.TotalAmount)
                        : query.OrderBy(i => i.TotalAmount),
                    "status" => filter.SortDescending == true 
                        ? query.OrderByDescending(i => i.Status)
                        : query.OrderBy(i => i.Status),
                    _ => query.OrderByDescending(i => i.CreatedAt)
                };
            }
            else
            {
                query = query.OrderByDescending(i => i.CreatedAt);
            }

            // Pagination
            if (filter.Page.HasValue && filter.PageSize.HasValue)
            {
                query = query.Skip((filter.Page.Value - 1) * filter.PageSize.Value)
                            .Take(filter.PageSize.Value);
            }

            return await query.ToListAsync();
        }

        public async Task<Invoice> UpdateInvoiceAsync(string id, InvoiceUpdateRequest request)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be updated");

            if (!string.IsNullOrEmpty(request.CustomerName))
                invoice.CustomerName = request.CustomerName;

            if (!string.IsNullOrEmpty(request.CustomerEmail))
                invoice.CustomerEmail = request.CustomerEmail;

            if (!string.IsNullOrEmpty(request.CustomerPhone))
                invoice.CustomerPhone = request.CustomerPhone;

            if (!string.IsNullOrEmpty(request.CustomerAddress))
                invoice.CustomerAddress = request.CustomerAddress;

            if (!string.IsNullOrEmpty(request.CustomerTaxNumber))
                invoice.CustomerTaxNumber = request.CustomerTaxNumber;

            if (request.InvoiceItems != null)
            {
                invoice.InvoiceItems = JsonSerializer.SerializeToDocument(request.InvoiceItems);
                // Recalculate totals
                var subtotal = request.InvoiceItems.Sum(item => item.TotalAmount);
                var taxAmount = request.InvoiceItems.Sum(item => item.TaxAmount);
                var totalAmount = subtotal + taxAmount;

                invoice.Subtotal = subtotal;
                invoice.TaxAmount = taxAmount;
                invoice.TotalAmount = totalAmount;
                invoice.RemainingAmount = totalAmount - invoice.PaidAmount;
            }

            if (!string.IsNullOrEmpty(request.Notes))
                invoice.Notes = request.Notes;

            if (!string.IsNullOrEmpty(request.TermsAndConditions))
                invoice.TermsAndConditions = request.TermsAndConditions;

            invoice.UpdatedById = request.UpdatedById;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice updated: {InvoiceNumber}", invoice.InvoiceNumber);
            return invoice;
        }

        public async Task DeleteInvoiceAsync(string id)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be deleted");

            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice deleted: {InvoiceNumber}", invoice.InvoiceNumber);
        }

        public async Task<Invoice> SendInvoiceAsync(string id, InvoiceSendRequest request)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status != InvoiceStatus.Draft)
                throw new InvalidOperationException("Only draft invoices can be sent");

            invoice.Status = InvoiceStatus.Sent;
            invoice.SentDate = DateTime.UtcNow;
            invoice.UpdatedById = request.SentById;
            invoice.UpdatedAt = DateTime.UtcNow;

            // TSE imzası ekle (gerçek uygulamada TSE servisi kullanılacak)
            invoice.TseSignature = GenerateTseSignature();
            invoice.KassenId = "KASSE-001";
            invoice.TseTimestamp = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice sent: {InvoiceNumber}", invoice.InvoiceNumber);
            return invoice;
        }

        public async Task<Invoice> MarkAsPaidAsync(string id, InvoicePaymentRequest request)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status == InvoiceStatus.Cancelled)
                throw new InvalidOperationException("Cancelled invoices cannot be marked as paid");

            invoice.PaidAmount += request.Amount;
            invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
            invoice.PaymentMethod = Enum.Parse<PaymentMethod>(request.PaymentMethod, true);
            invoice.PaymentReference = request.PaymentReference;
            invoice.PaymentDate = DateTime.UtcNow;
            invoice.UpdatedById = request.ProcessedById;
            invoice.UpdatedAt = DateTime.UtcNow;

            if (invoice.RemainingAmount <= 0)
            {
                invoice.Status = InvoiceStatus.Paid;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment recorded for invoice: {InvoiceNumber}, Amount: {Amount}", 
                invoice.InvoiceNumber, request.Amount);
            return invoice;
        }

        public async Task<Invoice> CancelInvoiceAsync(string id, string reason)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status == InvoiceStatus.Paid)
                throw new InvalidOperationException("Paid invoices cannot be cancelled");

            invoice.Status = InvoiceStatus.Cancelled;
            invoice.CancelledDate = DateTime.UtcNow;
            invoice.CancelledReason = reason;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Invoice cancelled: {InvoiceNumber}, Reason: {Reason}", 
                invoice.InvoiceNumber, reason);
            return invoice;
        }

        public async Task<bool> SubmitToFinanzOnlineAsync(string id)
        {
            var invoice = await GetInvoiceByIdAsync(id);

            if (invoice.Status != InvoiceStatus.Sent)
                throw new InvalidOperationException("Only sent invoices can be submitted to FinanzOnline");

            if (invoice.FinanzOnlineSubmitted)
                throw new InvalidOperationException("Invoice already submitted to FinanzOnline");

            try
            {
                // FinanzOnline'a gönderim
                var finOnlineInvoice = new FinanzOnlineInvoice {
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    TaxNumber = invoice.CompanyTaxNumber,
                    TseSignature = invoice.TseSignature,
                    CashRegisterId = invoice.KassenId,
                    Items = invoice.Items.ToList(),
                    TotalNet = invoice.Subtotal,
                    TotalTax = invoice.TaxAmount,
                    TotalGross = invoice.TotalAmount,
                    PaymentMethod = invoice.PaymentMethod?.ToString() ?? ""
                };
                var success = await _finanzOnlineService.SubmitInvoiceAsync(finOnlineInvoice);
                if (success)
                {
                    invoice.FinanzOnlineSubmitted = true;
                    invoice.FinanzOnlineSubmissionDate = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Invoice submitted to FinanzOnline: {InvoiceNumber}", invoice.InvoiceNumber);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit invoice to FinanzOnline: {InvoiceNumber}", 
                    invoice.InvoiceNumber);
                return false;
            }
        }

        public async Task<List<Invoice>> GetOverdueInvoicesAsync()
        {
            return await _context.Invoices
                .Where(i => i.Status == InvoiceStatus.Sent && i.DueDate < DateTime.UtcNow)
                .OrderBy(i => i.DueDate)
                .ToListAsync();
        }

        public async Task<InvoiceStatistics> GetInvoiceStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            var invoices = await _context.Invoices
                .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate)
                .ToListAsync();

            return new InvoiceStatistics
            {
                TotalInvoices = invoices.Count,
                TotalAmount = invoices.Sum(i => i.TotalAmount),
                PaidAmount = invoices.Sum(i => i.PaidAmount),
                OutstandingAmount = invoices.Sum(i => i.RemainingAmount),
                DraftCount = invoices.Count(i => i.Status == InvoiceStatus.Draft),
                SentCount = invoices.Count(i => i.Status == InvoiceStatus.Sent),
                PaidCount = invoices.Count(i => i.Status == InvoiceStatus.Paid),
                OverdueCount = invoices.Count(i => i.Status == InvoiceStatus.Sent && i.DueDate < DateTime.UtcNow),
                CancelledCount = invoices.Count(i => i.Status == InvoiceStatus.Cancelled)
            };
        }

        private string GenerateInvoiceNumber()
        {
            var date = DateTime.UtcNow;
            var year = date.Year;
            var month = date.Month.ToString("D2");
            var day = date.Day.ToString("D2");
            var random = new Random().Next(1000, 9999);
            
            return $"INV-{year}{month}{day}-{random}";
        }

        private string GenerateTseSignature()
        {
            // Gerçek uygulamada TSE cihazından imza alınacak
            return $"TSE-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        }
    }

    public class InvoiceCreateResponse
    {
        public Invoice Invoice { get; set; } = null!;
        public bool IsSubmittedToFinanzOnline { get; set; }
        public string? NetworkStatus { get; set; }
        public string? WarningMessage { get; set; }
        public bool HasPendingInvoices { get; set; }
        public int PendingCount { get; set; }
        public string? TseStatus { get; set; }
    }

    public class InvoiceCreateRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerTaxNumber { get; set; }
        public DateTime? DueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyAddress { get; set; } = string.Empty;
        public string? CompanyPhone { get; set; }
        public string? CompanyEmail { get; set; }
        public string CompanyTaxNumber { get; set; } = string.Empty;
        public string? TermsAndConditions { get; set; }
        public string? Notes { get; set; }
        public List<InvoiceItem> InvoiceItems { get; set; } = new();
        public string? CustomerId { get; set; }
        public string CreatedById { get; set; } = string.Empty;
    }

    public class InvoiceUpdateRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerAddress { get; set; }
        public string? CustomerTaxNumber { get; set; }
        public List<InvoiceItem>? InvoiceItems { get; set; }
        public string? Notes { get; set; }
        public string? TermsAndConditions { get; set; }
        public string? UpdatedById { get; set; }
    }

    public class InvoicePaymentRequest
    {
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PaymentReference { get; set; }
        public string ProcessedById { get; set; } = string.Empty;
    }

    public class InvoiceFilterRequest
    {
        public string? Status { get; set; }
        public string? CustomerId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public string? SortBy { get; set; }
        public bool? SortDescending { get; set; }
        public int? Page { get; set; }
        public int? PageSize { get; set; }
    }

    public class InvoiceStatistics
    {
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public int DraftCount { get; set; }
        public int SentCount { get; set; }
        public int PaidCount { get; set; }
        public int OverdueCount { get; set; }
        public int CancelledCount { get; set; }
    }
} 
