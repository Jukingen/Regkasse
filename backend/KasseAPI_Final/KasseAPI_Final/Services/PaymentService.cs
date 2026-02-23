using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Data.Repositories;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Ödeme işlemleri için service implementation
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly IGenericRepository<PaymentDetails> _paymentRepository;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IGenericRepository<Customer> _customerRepository;
        private readonly ITseService _tseService;
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly ILogger<PaymentService> _logger;
        private readonly IUserService _userService; // Kullanıcı rol kontrolü için
        private readonly CompanyProfileOptions _companyProfile;

        public PaymentService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IGenericRepository<Product> productRepository,
            IGenericRepository<Customer> customerRepository,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            IUserService userService,
            Microsoft.Extensions.Options.IOptions<CompanyProfileOptions> companyProfile,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _userService = userService;
            _companyProfile = companyProfile.Value;
            _logger = logger;
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        public async Task<PaymentResult> CreatePaymentAsync(CreatePaymentRequest request, string userId)
        {
            try
            {
                _logger.LogInformation("Creating payment for customer {CustomerId} by user {UserId}", request.CustomerId, userId);

                // Demo kullanıcı kontrolü - demo kullanıcılar gerçek ödeme oluşturamaz
                var user = await _userService.GetUserByIdAsync(userId);
                if (user?.Role == "Demo")
                {
                    _logger.LogWarning("Demo user {UserId} attempted to create real payment", userId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot create real payments",
                        Errors = { "Demo users are restricted to test operations only" }
                    };
                }

                // Müşteri kontrolü
                var customer = await _customerRepository.GetByIdAsync(request.CustomerId);
                if (customer == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Customer not found",
                        Errors = { "Customer not found" }
                    };
                }

                // Steuernummer (request) kontrolü - ATU formatı
                if (!IsValidAustrianTaxNumber(request.Steuernummer))
                {
                    _logger.LogWarning("Invalid Austrian tax number in request: {TaxNumber}", request.Steuernummer);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Invalid Austrian tax number format",
                        Errors = { "Steuernummer must be in ATU format (e.g., ATU12345678)" }
                    };
                }

                // TSE cihaz kontrolü (eğer TSE gerekliyse)
                if (request.Payment.TseRequired)
                {
                    var tseStatus = await _tseService.GetDeviceStatusAsync();
                    if (!tseStatus.IsConnected)
                    {
                        _logger.LogError("TSE device not connected. Cannot create payment requiring TSE signature");
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "TSE device not connected",
                            Errors = { "TSE device must be connected for this payment type" }
                        };
                    }
                    
                    if (!tseStatus.IsReady)
                    {
                        _logger.LogError("TSE device not ready. Status: {Status}", tseStatus.Status);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "TSE device not ready",
                            Errors = { $"TSE device is not ready. Status: {tseStatus.Status}" }
                        };
                    }
                }

                // Ürün kontrolü ve stok güncelleme
                var paymentItems = new List<PaymentItem>();
                decimal totalAmount = 0;
                var taxDetails = new Dictionary<string, decimal>();

                foreach (var itemRequest in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(itemRequest.ProductId);
                    if (product == null)
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Product not found",
                            Errors = { $"Product with ID {itemRequest.ProductId} not found" }
                        };
                    }

                    if (product.StockQuantity < itemRequest.Quantity)
                    {
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Insufficient stock",
                            Errors = { $"Insufficient stock for product {product.Name}" }
                        };
                    }

                    // Stok güncelle - transaction içinde yapılmalı
                    product.StockQuantity -= itemRequest.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    await _productRepository.UpdateAsync(product);

                    // Ödeme kalemi oluştur
                    var itemAmount = product.Price * itemRequest.Quantity;
                    totalAmount += itemAmount;

                    var paymentItem = new PaymentItem
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = product.Price,
                        TotalPrice = itemAmount,
                        TaxType = itemRequest.TaxType,
                        TaxRate = GetTaxRate(itemRequest.TaxType),
                        TaxAmount = CalculateTax(itemAmount, itemRequest.TaxType)
                    };

                    paymentItems.Add(paymentItem);

                    // Vergi detayları
                    var taxKey = itemRequest.TaxType.ToString();
                    if (!taxDetails.ContainsKey(taxKey))
                        taxDetails[taxKey] = 0;
                    taxDetails[taxKey] += paymentItem.TaxAmount;
                }

                // Ödeme detayları oluştur
                var payment = new PaymentDetails
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(paymentItems)),
                    TotalAmount = totalAmount,
                    TaxAmount = taxDetails.Values.Sum(),
                    TaxDetails = JsonDocument.Parse(JsonSerializer.Serialize(taxDetails)),
                    PaymentMethodRaw = GetPaymentMethodEnum(request.Payment.Method),
                    Notes = request.Notes,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    // Yeni alanlar
                    TableNumber = request.TableNumber,
                    CashierId = request.CashierId,
                    Steuernummer = request.Steuernummer,
                    KassenId = request.KassenId,
                    TseTimestamp = DateTime.UtcNow,
                    IsPrinted = false,
                    ReceiptNumber = string.Empty
                };

                // TSE imzası oluştur (eğer gerekliyse)
                if (request.Payment.TseRequired)
                {
                    try
                    {
                        payment.TseSignature = await _tseService.CreateInvoiceSignatureAsync(
                            Guid.NewGuid(), // cashRegisterId - gerçek implementasyonda bu değer alınmalı
                            payment.Id.ToString(), // invoiceNumber
                            payment.TotalAmount);
                        _logger.LogInformation("TSE signature generated for payment {PaymentId}", payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate TSE signature for payment {PaymentId}", payment.Id);
                        return new PaymentResult
                        {
                            Success = false,
                            Message = "Failed to generate TSE signature",
                            Errors = { "TSE signature generation failed" }
                        };
                    }
                }

                // Ödemeyi kaydet
                var createdPayment = await _paymentRepository.AddAsync(payment);

                // ReceiptNumber'ı kuralına göre set et (AT-{KassenId}-{YYYYMMDD}-{SEQ})
                if (string.IsNullOrEmpty(createdPayment.ReceiptNumber))
                {
                    var seq = createdPayment.Id.ToString("N").Substring(0, 8);
                    createdPayment.ReceiptNumber = $"AT-{createdPayment.KassenId}-{DateTime.UtcNow:yyyyMMdd}-{seq}";
                    await _paymentRepository.UpdateAsync(createdPayment);
                }

                // Persist a canonical Invoice row so that list / detail / PDF all share the same domain and ID
                try
                {
                    // Idempotency check: skip if an invoice already exists for this payment
                    var existingInvoice = await _context.Invoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.SourcePaymentId == createdPayment.Id);

                    if (existingInvoice == null)
                    {
                        var companyAddress = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}";
                        Guid? resolvedCashRegisterId = null;
                        if (!string.IsNullOrWhiteSpace(createdPayment.KassenId))
                        {
                            if (Guid.TryParse(createdPayment.KassenId, out var parsedRegId))
                            {
                                var crExists = await _context.CashRegisters.AnyAsync(CR => CR.Id == parsedRegId);
                                if (crExists) resolvedCashRegisterId = parsedRegId;
                            }

                            if (resolvedCashRegisterId == null)
                            {
                                var register = await _context.CashRegisters.FirstOrDefaultAsync(cr => cr.RegisterNumber == createdPayment.KassenId);
                                resolvedCashRegisterId = register?.Id;
                            }
                        }

                        if (resolvedCashRegisterId == null)
                        {
                            _logger.LogWarning("Could not resolve real CashRegisterId for KassenId '{KassenId}' during payment invoice creation. Saving without CashRegisterId, Tagesabschluss may miss this.", createdPayment.KassenId);
                        }

                        var posInvoice = new Invoice
                        {
                            Id = Guid.NewGuid(),
                            SourcePaymentId = createdPayment.Id,
                            InvoiceNumber = createdPayment.ReceiptNumber,
                            InvoiceDate = createdPayment.CreatedAt,
                            DueDate = createdPayment.CreatedAt,
                            Status = InvoiceStatus.Paid,
                            Subtotal = createdPayment.TotalAmount - createdPayment.TaxAmount,
                            TaxAmount = createdPayment.TaxAmount,
                            TotalAmount = createdPayment.TotalAmount,
                            PaidAmount = createdPayment.TotalAmount,
                            RemainingAmount = 0,
                            CustomerName = createdPayment.CustomerName,
                            CustomerTaxNumber = createdPayment.Steuernummer,
                            CompanyName = _companyProfile.CompanyName,
                            CompanyTaxNumber = _companyProfile.TaxNumber,
                            CompanyAddress = companyAddress,
                            TseSignature = createdPayment.TseSignature ?? string.Empty,
                            KassenId = createdPayment.KassenId,
                            TseTimestamp = createdPayment.TseTimestamp,
                            CashRegisterId = resolvedCashRegisterId,
                            PaymentMethod = createdPayment.PaymentMethod,
                            PaymentReference = createdPayment.TransactionId,
                            PaymentDate = createdPayment.CreatedAt,
                            InvoiceItems = createdPayment.PaymentItems,
                            TaxDetails = createdPayment.TaxDetails,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.Invoices.Add(posInvoice);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Invoice persisted for payment {PaymentId}: InvoiceId={InvoiceId}, InvoiceNumber={InvoiceNumber}",
                            createdPayment.Id, posInvoice.Id, posInvoice.InvoiceNumber);

                        // FinanzOnline'a gönder (TSE gerekiyorsa)
                        if (request.Payment.TseRequired)
                        {
                            try
                            {
                                await _finanzOnlineService.SubmitInvoiceAsync(posInvoice);
                                _logger.LogInformation("Invoice sent to FinanzOnline: {InvoiceId}", posInvoice.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send invoice to FinanzOnline: {InvoiceId}", posInvoice.Id);
                                // FinanzOnline hatası ödeme oluşturmayı engellemez
                            }
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Invoice already exists for payment {PaymentId} — skipping duplicate insert", createdPayment.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Invoice persist hatası ödeme oluşturmayı engellemez — PaymentDetails kaydı zaten var
                    _logger.LogError(ex, "Failed to persist Invoice for payment {PaymentId}", createdPayment.Id);
                }

                _logger.LogInformation("Payment created successfully: {PaymentId} for customer {CustomerId}", 
                    createdPayment.Id, customer.Id);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Payment created successfully",
                    Payment = createdPayment,
                    PaymentId = createdPayment.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment for customer {CustomerId}", request.CustomerId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while creating payment",
                    Errors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        public async Task<PaymentDetails?> GetPaymentAsync(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogDebug("Payment not found with ID: {PaymentId}", paymentId);
                    return null;
                }

                _logger.LogDebug("Payment retrieved successfully: {PaymentId}", paymentId);
                return payment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", paymentId);
                return null;
            }
        }

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetCustomerPaymentsAsync(Guid customerId, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.CustomerId == customerId && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for customer {CustomerId} (page {PageNumber}/{TotalPages})", 
                    items.Count(), customerId, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for customer {CustomerId}", customerId);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByMethodAsync(string paymentMethod, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Ödeme yöntemi validasyonu
                var validMethods = new[] { "cash", "card", "voucher" };
                if (!validMethods.Contains(paymentMethod.ToLower()))
                {
                    _logger.LogWarning("Invalid payment method: {PaymentMethod}", paymentMethod);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.PaymentMethodRaw == GetPaymentMethodEnum(paymentMethod) && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for method {PaymentMethod} (page {PageNumber}/{TotalPages})", 
                    items.Count(), paymentMethod, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by method {PaymentMethod}", paymentMethod);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        public async Task<IEnumerable<PaymentDetails>> GetPaymentsByDateRangeAsync(DateTime startDate, DateTime endDate, int pageNumber = 1, int pageSize = 20)
        {
            try
            {
                // Tarih validasyonu
                if (startDate > endDate)
                {
                    _logger.LogWarning("Invalid date range: startDate {StartDate} is after endDate {EndDate}", startDate, endDate);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Maksimum tarih aralığı kontrolü (7 yıl)
                var maxDateRange = TimeSpan.FromDays(365 * 7);
                if (endDate - startDate > maxDateRange)
                {
                    _logger.LogWarning("Date range too large: {Days} days. Maximum allowed: {MaxDays} days", 
                        (endDate - startDate).Days, maxDateRange.Days);
                    return Enumerable.Empty<PaymentDetails>();
                }

                // Sayfa boyutu validasyonu
                if (pageSize <= 0 || pageSize > 100)
                {
                    _logger.LogWarning("Invalid page size: {PageSize}. Using default value 20", pageSize);
                    pageSize = 20;
                }

                if (pageNumber <= 0)
                {
                    _logger.LogWarning("Invalid page number: {PageNumber}. Using default value 1", pageNumber);
                    pageNumber = 1;
                }

                var (items, totalCount) = await _paymentRepository.GetPagedAsync(
                    pageNumber, 
                    pageSize, 
                    p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.IsActive,
                    p => p.CreatedAt,
                    false);

                _logger.LogDebug("Retrieved {Count} payments for date range {StartDate} to {EndDate} (page {PageNumber}/{TotalPages})", 
                    items.Count(), startDate, endDate, pageNumber, Math.Ceiling((double)totalCount / pageSize));

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by date range {StartDate} to {EndDate}", startDate, endDate);
                return Enumerable.Empty<PaymentDetails>();
            }
        }

        /// <summary>
        /// Ödeme iptal et
        /// </summary>
        public async Task<PaymentResult> CancelPaymentAsync(Guid paymentId, string reason, string userId)
        {
            try
            {
                // Demo kullanıcı kontrolü
                var user = await _userService.GetUserByIdAsync(userId);
                if (user?.Role == "Demo")
                {
                    _logger.LogWarning("Demo user {UserId} attempted to cancel payment {PaymentId}", userId, paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot cancel real payments",
                        Errors = { "Demo users are restricted to test operations only" }
                    };
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                // Zaten iptal edilmiş ödeme kontrolü
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment is already cancelled",
                        Errors = { "Payment has already been cancelled" }
                    };
                }

                // TSE imzası varsa iptal et
                if (!string.IsNullOrEmpty(payment.TseSignature))
                {
                    try
                    {
                        await _tseService.CancelInvoiceSignatureAsync(payment.TseSignature);
                        _logger.LogInformation("TSE signature cancelled for payment {PaymentId}", paymentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cancel TSE signature for payment {PaymentId}", paymentId);
                        // TSE imza iptali başarısız olsa bile ödeme iptal edilebilir
                    }
                }

                // Stok geri ekle - transaction içinde yapılmalı
                var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText());
                if (paymentItems != null)
                {
                    foreach (var item in paymentItems)
                    {
                        var product = await _productRepository.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                            await _productRepository.UpdateAsync(product);
                        }
                    }
                }

                // Ödemeyi iptal et
                payment.IsActive = false;
                payment.UpdatedAt = DateTime.UtcNow;
                payment.UpdatedBy = userId;
                // TODO: CancellationReason ve CancelledAt alanları eklenecek

                await _paymentRepository.UpdateAsync(payment);

                _logger.LogInformation("Payment {PaymentId} cancelled by user {UserId} with reason: {Reason}", 
                    paymentId, userId, reason);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Payment cancelled successfully",
                    Payment = payment
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while cancelling payment",
                    Errors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Ödeme iade et
        /// </summary>
        public async Task<PaymentResult> RefundPaymentAsync(Guid paymentId, decimal amount, string reason, string userId)
        {
            try
            {
                // Demo kullanıcı kontrolü
                var user = await _userService.GetUserByIdAsync(userId);
                if (user?.Role == "Demo")
                {
                    _logger.LogWarning("Demo user {UserId} attempted to refund payment {PaymentId}", userId, paymentId);
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Demo users cannot refund real payments",
                        Errors = { "Demo users are restricted to test operations only" }
                    };
                }

                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Payment not found",
                        Errors = { "Payment not found" }
                    };
                }

                // Zaten iptal edilmiş ödeme kontrolü
                if (!payment.IsActive)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Cannot refund cancelled payment",
                        Errors = { "Payment has been cancelled and cannot be refunded" }
                    };
                }

                // İade tutarı kontrolü
                if (amount > payment.TotalAmount)
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Message = "Refund amount cannot exceed payment amount",
                        Errors = { "Refund amount exceeds payment amount" }
                    };
                }

                // Kısmi iade için stok güncelleme
                if (amount < payment.TotalAmount)
                {
                    var refundRatio = amount / payment.TotalAmount;
                    var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.RootElement.GetRawText());
                    if (paymentItems != null)
                    {
                        foreach (var item in paymentItems)
                        {
                            var product = await _productRepository.GetByIdAsync(item.ProductId);
                            if (product != null)
                            {
                                var refundQuantity = (int)(item.Quantity * refundRatio);
                                product.StockQuantity += refundQuantity;
                                product.UpdatedAt = DateTime.UtcNow;
                                await _productRepository.UpdateAsync(product);
                            }
                        }
                    }
                }

                // İade kaydı oluştur
                var refund = new PaymentDetails
                {
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.CustomerName,
                    PaymentItems = payment.PaymentItems, // JSON olarak kopyala
                    TotalAmount = -amount, // Negatif tutar
                    TaxAmount = -payment.TaxAmount * (amount / payment.TotalAmount),
                    PaymentMethod = payment.PaymentMethod,
                    Notes = $"Refund: {reason}",
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                    // TODO: IsRefund, OriginalPaymentId, RefundReason, RefundAmount alanları eklenecek
                };

                await _paymentRepository.AddAsync(refund);

                _logger.LogInformation("Refund created for payment {PaymentId} by user {UserId} for amount {Amount}", 
                    paymentId, userId, amount);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Refund processed successfully",
                    Payment = refund
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for payment {PaymentId}", paymentId);
                return new PaymentResult
                {
                    Success = false,
                    Message = "An error occurred while processing refund",
                    Errors = { ex.Message }
                };
            }
        }

        /// <summary>
        /// Get payment statistics for date range
        /// </summary>
        public async Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            // Initialize DTO with request dates immediately (never returns DateTime.MinValue)
            var statistics = new PaymentStatistics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalPayments = 0,
                TotalAmount = 0,
                AverageAmount = 0,
                PaymentsByMethod = new Dictionary<string, int>(),
                AmountByMethod = new Dictionary<string, decimal>(),
                PaymentsByTaxType = new Dictionary<string, int>(),
                TseSignedPayments = 0,
                TseSignedAmount = 0
            };

            // Query using PaymentMethodRaw - no InvalidCastException since it's varchar
            var payments = await _context.PaymentDetails
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.IsActive)
                .ToListAsync();

            // Diagnostic log to confirm PaymentMethodRaw reads as string
            if (payments.Any())
            {
                var firstPayment = payments.First();
                _logger.LogInformation("DIAGNOSTIC: PaymentMethodRaw type={Type}, value={Value}", 
                    firstPayment.PaymentMethodRaw?.GetType().Name ?? "null", 
                    firstPayment.PaymentMethodRaw ?? "null");
            }

            if (!payments.Any())
            {
                _logger.LogInformation("No payments found for period {StartDate} to {EndDate}", startDate, endDate);
                return statistics;
            }

            // Calculate basic statistics
            statistics.TotalPayments = payments.Count;
            statistics.TotalAmount = payments.Sum(p => p.TotalAmount);
            statistics.AverageAmount = payments.Average(p => p.TotalAmount);

            // Group by payment method - parse varchar numeric strings to enum names
            var paymentMethodGroups = payments
                .Select(p => new 
                { 
                    Payment = p,
                    MethodName = ParsePaymentMethodName(p.PaymentMethodRaw)
                })
                .GroupBy(x => x.MethodName)
                .ToList();

            statistics.PaymentsByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Count());

            statistics.AmountByMethod = paymentMethodGroups
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Payment.TotalAmount));

            // Group by tax type (safe JSON parsing)
            foreach (var payment in payments)
            {
                try 
                {
                    if (payment.PaymentItems != null && payment.PaymentItems.RootElement.ValueKind != JsonValueKind.Undefined)
                    {
                        var jsonText = payment.PaymentItems.RootElement.GetRawText();
                        var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(jsonText);
                        
                        if (paymentItems != null)
                        {
                            foreach (var item in paymentItems)
                            {
                                var taxType = item.TaxType.ToString();
                                if (!statistics.PaymentsByTaxType.ContainsKey(taxType))
                                    statistics.PaymentsByTaxType[taxType] = 0;
                                statistics.PaymentsByTaxType[taxType]++;
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Failed to deserialize payment items for payment {PaymentId}", payment.Id);
                }
            }

            // TSE statistics
            var tsePayments = payments.Where(p => !string.IsNullOrEmpty(p.TseSignature)).ToList();
            statistics.TseSignedPayments = tsePayments.Count;
            statistics.TseSignedAmount = tsePayments.Sum(p => p.TotalAmount);

            _logger.LogInformation("Payment statistics: {StartDate} to {EndDate}, {Count} payments, {Amount} total", 
                startDate, endDate, statistics.TotalPayments, statistics.TotalAmount);

            return statistics;
            // No try-catch - exceptions propagate to controller for proper 500 response
        }

        /// <summary>
        /// Parse varchar payment method string ('0', '1', etc.) to readable enum name
        /// </summary>
        private string ParsePaymentMethodName(string rawValue)
        {
            if (int.TryParse(rawValue, out int methodInt) && Enum.IsDefined(typeof(PaymentMethod), methodInt))
            {
                return ((PaymentMethod)methodInt).ToString();
            }
            return "Unknown";
        }

        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        public async Task<string> GenerateTseSignatureAsync(PaymentDetails payment)
        {
            try
            {
                // TSE cihaz durumu kontrolü
                var tseStatus = await _tseService.GetDeviceStatusAsync();
                if (!tseStatus.IsConnected)
                {
                    _logger.LogError("TSE device not connected. Cannot generate signature for payment {PaymentId}", payment.Id);
                    throw new InvalidOperationException("TSE device is not connected");
                }

                // TSE cihazı hazır mı kontrolü
                if (!tseStatus.IsReady)
                {
                    _logger.LogWarning("TSE device not ready. Status: {Status}", tseStatus.Status);
                    throw new InvalidOperationException($"TSE device is not ready. Status: {tseStatus.Status}");
                }

                var signature = await _tseService.CreateInvoiceSignatureAsync(
                    Guid.NewGuid(), // cashRegisterId - gerçek implementasyonda bu değer alınmalı
                    payment.Id.ToString(), // invoiceNumber
                    payment.TotalAmount);

                _logger.LogInformation("TSE signature generated successfully for payment {PaymentId}: {Signature}", 
                    payment.Id, signature);

                return signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TSE signature for payment {PaymentId}", payment.Id);
                throw;
            }
        }

        /// <summary>
        /// FinanzOnline entegrasyonu
        /// </summary>
        public async Task<bool> SendToFinanzOnlineAsync(PaymentDetails payment)
        {
            try
            {
                // TSE imzası kontrolü - FinanzOnline için zorunlu
                if (string.IsNullOrEmpty(payment.TseSignature))
                {
                    _logger.LogWarning("Payment {PaymentId} has no TSE signature. Cannot send to FinanzOnline", payment.Id);
                    return false;
                }

                // PaymentDetails'den Invoice oluştur
                var invoice = new Invoice
                {
                    InvoiceNumber = payment.Id.ToString(),
                    InvoiceDate = payment.CreatedAt,
                    DueDate = payment.CreatedAt.AddDays(30),
                    Status = InvoiceStatus.Paid,
                    Subtotal = payment.TotalAmount - payment.TaxAmount,
                    TaxAmount = payment.TaxAmount,
                    TotalAmount = payment.TotalAmount,
                    PaidAmount = payment.TotalAmount,
                    RemainingAmount = 0,
                    CustomerName = payment.CustomerName,
                    CompanyName = "Company Name", // Gerçek implementasyonda config'den alınmalı
                    CompanyTaxNumber = "ATU12345678", // Gerçek implementasyonda config'den alınmalı
                    CompanyAddress = "Company Address", // Gerçek implementasyonda config'den alınmalı
                    TseSignature = payment.TseSignature,
                    KassenId = "KASSE001", // Gerçek implementasyonda config'den alınmalı
                    TseTimestamp = payment.CreatedAt,
                    CashRegisterId = Guid.NewGuid(), // Gerçek implementasyonda config'den alınmalı
                    PaymentMethod = payment.PaymentMethod,
                    PaymentReference = payment.TransactionId,
                    PaymentDate = payment.CreatedAt
                };

                var result = await _finanzOnlineService.SubmitInvoiceAsync(invoice);
                
                if (result.Success)
                {
                    _logger.LogInformation("Payment {PaymentId} successfully sent to FinanzOnline", payment.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to send payment {PaymentId} to FinanzOnline: {Error}", 
                        payment.Id, result.ErrorMessage);
                }

                return result.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending payment to FinanzOnline: {PaymentId}", payment.Id);
                return false;
            }
        }

        /// <summary>
        /// Get formatted receipt data for payment
        /// </summary>
        public async Task<ReceiptDTO?> GetReceiptDataAsync(Guid paymentId)
        {
            try
            {
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment not found for receipt: {PaymentId}", paymentId);
                    return null;
                }

                // Deserialize payment items
                var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(
                    payment.PaymentItems.RootElement.GetRawText());

                if (paymentItems == null)
                {
                    _logger.LogError("Failed to deserialize payment items for payment {PaymentId}", paymentId);
                    return null;
                }

                // Get cashier name
                var cashier = await _userService.GetUserByIdAsync(payment.CreatedBy);
                var cashierName = cashier?.Name ?? cashier?.UserName ?? "Unknown";

                // Map payment items to receipt items
                var receiptItems = paymentItems.Select(item => new ReceiptItemDTO
                {
                    Name = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    TaxRate = item.TaxRate * 100 // Convert 0.20 to 20.0
                }).ToList();

                // Calculate subtotal (total - tax)
                var subtotal = payment.TotalAmount - payment.TaxAmount;

                // Calculate Tax Rates Breakdown
                var taxRates = paymentItems
                    .GroupBy(i => i.TaxRate)
                    .Select(g => new ReceiptTaxLineDTO
                    {
                        Rate = g.Key * 100,
                        TaxAmount = g.Sum(x => x.TaxAmount),
                        NetAmount = g.Sum(x => x.TotalPrice - x.TaxAmount),
                        GrossAmount = g.Sum(x => x.TotalPrice)
                    })
                    .ToList();

                var receiptDTO = new ReceiptDTO
                {
                    ReceiptId = payment.Id, // Use PaymentId as ephemeral ReceiptId
                    ReceiptNumber = payment.ReceiptNumber ?? "DRAFT",
                    Date = payment.CreatedAt,
                    CashierName = cashierName,
                    TableNumber = payment.TableNumber,
                    KassenID = payment.KassenId ?? "KASSE01",
                    
                    Company = new ReceiptCompanyDTO
                    {
                        Name = _companyProfile.CompanyName,
                        Address = $"{_companyProfile.Street}, {_companyProfile.ZipCode} {_companyProfile.City}",
                        TaxNumber = !string.IsNullOrEmpty(payment.Steuernummer) ? payment.Steuernummer : _companyProfile.TaxNumber
                    },
                    
                    Header = new ReceiptHeaderDTO
                    {
                        ShopName = _companyProfile.CompanyName,
                        Address = $"{_companyProfile.Street}, {_companyProfile.City}"
                    },

                    Items = receiptItems,
                    
                    SubTotal = subtotal,
                    TaxAmount = payment.TaxAmount, // Fixed prop name
                    GrandTotal = payment.TotalAmount,
                    
                    TaxRates = taxRates,
                    
                    Payments = new List<ReceiptPaymentDTO>
                    {
                        new ReceiptPaymentDTO
                        {
                            Method = payment.PaymentMethodRaw.ToString(),
                            Amount = payment.TotalAmount,
                            Tendered = payment.TotalAmount, // Assuming exact amount for now
                            Change = 0
                        }
                    },
                    
                    FooterText = _companyProfile.FooterText,
                    
                    Signature = !string.IsNullOrEmpty(payment.TseSignature) ? new ReceiptSignatureDTO
                    {
                        Algorithm = "ES256", // Demo
                        Value = payment.TseSignature,
                        SerialNumber = "DEMO-SERIAL-123",
                        Timestamp = payment.TseTimestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                        QrData = payment.TseSignature // Demo payload
                    } : null
                };

                _logger.LogInformation("Receipt data generated for payment {PaymentId}", paymentId);
                return receiptDTO;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt data for payment {PaymentId}", paymentId);
                return null;
            }
        }

        #region Private Methods

        private decimal GetTaxRate(int taxType)
        {
            return TaxTypes.GetTaxRate(taxType) / 100.0m; // Convert 20.0 to 0.20
        }

        private decimal CalculateTax(decimal amount, int taxType)
        {
            var taxRate = GetTaxRate(taxType);
            return amount * taxRate;
        }

        /// <summary>
        /// Convert payment method string to DB format (numeric string)
        /// </summary>
        private string GetPaymentMethodEnum(string paymentMethod)
        {
            // Map common payment method strings to numeric strings
            return paymentMethod?.ToLower() switch
            {
                "cash" => "0",
                "card" => "1",
                "banktransfer" => "2",
                "transfer" => "2",
                "check" => "3",
                "voucher" => "4",
                "mobile" => "5",
                _ => "0" // Default to Cash
            };
        }

        private bool IsValidAustrianTaxNumber(string taxNumber)
        {
            // ATU formatı: ATU + 8 haneli sayı
            var pattern = @"^ATU\d{8}$";
            return Regex.IsMatch(taxNumber, pattern);
        }

        #endregion
    }
}
