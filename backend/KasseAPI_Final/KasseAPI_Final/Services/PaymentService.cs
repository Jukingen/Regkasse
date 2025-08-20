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

        public PaymentService(
            AppDbContext context,
            IGenericRepository<PaymentDetails> paymentRepository,
            IGenericRepository<Product> productRepository,
            IGenericRepository<Customer> customerRepository,
            ITseService tseService,
            IFinanzOnlineService finanzOnlineService,
            IUserService userService,
            ILogger<PaymentService> logger)
        {
            _context = context;
            _paymentRepository = paymentRepository;
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _tseService = tseService;
            _finanzOnlineService = finanzOnlineService;
            _userService = userService;
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
                    var taxKey = itemRequest.TaxType;
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
                    PaymentMethod = GetPaymentMethodEnum(request.Payment.Method),
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

                // FinanzOnline'a gönder (Invoice olarak)
                if (request.Payment.TseRequired)
                {
                    try
                    {
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
                            TseSignature = payment.TseSignature ?? string.Empty,
                            KassenId = "KASSE001", // Gerçek implementasyonda config'den alınmalı
                            TseTimestamp = payment.CreatedAt,
                            CashRegisterId = Guid.NewGuid(), // Gerçek implementasyonda config'den alınmalı
                            PaymentMethod = payment.PaymentMethod,
                            PaymentReference = payment.TransactionId,
                            PaymentDate = payment.CreatedAt
                        };

                        await _finanzOnlineService.SubmitInvoiceAsync(invoice);
                        _logger.LogInformation("Payment sent to FinanzOnline as Invoice: {PaymentId}", payment.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send payment to FinanzOnline: {PaymentId}", payment.Id);
                        // FinanzOnline hatası ödeme oluşturmayı engellemez
                    }
                }

                _logger.LogInformation("Payment created successfully: {PaymentId} for customer {CustomerId}", 
                    createdPayment.Id, customer.Id);

                return new PaymentResult
                {
                    Success = true,
                    Message = "Payment created successfully",
                    Payment = createdPayment
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
                    p => p.PaymentMethod == GetPaymentMethodEnum(paymentMethod) && p.IsActive,
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
                var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.ToString());
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
                    var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.ToString());
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
        /// Ödeme istatistiklerini getir
        /// </summary>
        public async Task<PaymentStatistics> GetPaymentStatisticsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                // Tarih aralığı validasyonu
                if (startDate > endDate)
                {
                    _logger.LogWarning("Invalid date range: startDate {StartDate} is after endDate {EndDate}", startDate, endDate);
                    return new PaymentStatistics();
                }

                // Maksimum tarih aralığı kontrolü (7 yıl)
                var maxDateRange = TimeSpan.FromDays(365 * 7);
                if (endDate - startDate > maxDateRange)
                {
                    _logger.LogWarning("Date range too large: {Days} days. Maximum allowed: {MaxDays} days", 
                        (endDate - startDate).Days, maxDateRange.Days);
                    return new PaymentStatistics();
                }

                var payments = await _context.PaymentDetails
                    .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.IsActive)
                    .ToListAsync();

                var statistics = new PaymentStatistics
                {
                    TotalPayments = payments.Count,
                    TotalAmount = payments.Sum(p => p.TotalAmount),
                    AverageAmount = payments.Any() ? payments.Average(p => p.TotalAmount) : 0,
                    StartDate = startDate,
                    EndDate = endDate
                };

                // Ödeme yöntemine göre grupla
                statistics.PaymentsByMethod = payments
                    .GroupBy(p => p.PaymentMethod)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());

                statistics.AmountByMethod = payments
                    .GroupBy(p => p.PaymentMethod)
                    .ToDictionary(g => g.Key.ToString(), g => g.Sum(p => p.TotalAmount));

                // Vergi tipine göre grupla
                foreach (var payment in payments)
                {
                    var paymentItems = JsonSerializer.Deserialize<List<PaymentItem>>(payment.PaymentItems.ToString());
                    if (paymentItems != null)
                    {
                        foreach (var item in paymentItems)
                        {
                            var taxType = item.TaxType;
                            if (!statistics.PaymentsByTaxType.ContainsKey(taxType))
                                statistics.PaymentsByTaxType[taxType] = 0;
                            statistics.PaymentsByTaxType[taxType]++;
                        }
                    }
                }

                // TSE imzalı ödemeler
                var tsePayments = payments.Where(p => !string.IsNullOrEmpty(p.TseSignature)).ToList();
                statistics.TseSignedPayments = tsePayments.Count;
                statistics.TseSignedAmount = tsePayments.Sum(p => p.TotalAmount);

                // FinanzOnline'a gönderilen ödemeler
                // TODO: IsFinanzOnlineSent alanı eklenecek
                statistics.FinanzOnlineSentPayments = 0;
                statistics.FinanzOnlineSentAmount = 0;

                _logger.LogInformation("Payment statistics generated for period {StartDate} to {EndDate}: {TotalPayments} payments, {TotalAmount} total", 
                    startDate, endDate, statistics.TotalPayments, statistics.TotalAmount);

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment statistics from {StartDate} to {EndDate}", startDate, endDate);
                return new PaymentStatistics();
            }
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

        #region Private Methods

        private decimal GetTaxRate(string taxType)
        {
            return taxType.ToLower() switch
            {
                "standard" => 0.20m, // %20
                "reduced" => 0.10m,  // %10
                "special" => 0.13m,  // %13
                _ => 0.20m
            };
        }

        private decimal CalculateTax(decimal amount, string taxType)
        {
            var taxRate = GetTaxRate(taxType);
            return amount * taxRate;
        }

        private PaymentMethod GetPaymentMethodEnum(string paymentMethod)
        {
            return paymentMethod.ToLower() switch
            {
                "cash" => PaymentMethod.Cash,
                "card" => PaymentMethod.Card,
                "voucher" => PaymentMethod.Voucher,
                _ => PaymentMethod.Cash
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
