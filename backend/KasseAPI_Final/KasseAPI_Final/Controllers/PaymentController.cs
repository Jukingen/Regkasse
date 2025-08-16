using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text;

namespace KasseAPI_Final.Controllers
{
    [Authorize] // Tüm controller için authentication gerekli
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentController> _logger;
        private readonly IReceiptService _receiptService;
        private readonly IAuditLogService _auditLogService;

        public PaymentController(AppDbContext context, ILogger<PaymentController> logger, IReceiptService receiptService, IAuditLogService auditLogService)
        {
            _context = context;
            _logger = logger;
            _receiptService = receiptService;
            _auditLogService = auditLogService;
        }

        // Kapsamlı ödeme loglama sistemi
        private async Task LogPaymentAttempt(PaymentLogEntry logEntry)
        {
            try
            {
                // Veritabanına log kaydı ekle
                _context.PaymentLogs.Add(logEntry);
                await _context.SaveChangesAsync();

                // Console ve dosyaya da logla
                var logMessage = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                switch (logEntry.Status)
                {
                    case PaymentLogStatus.Success:
                        _logger.LogInformation("PAYMENT_SUCCESS: {LogMessage}", logMessage);
                        break;
                    case PaymentLogStatus.Failed:
                        _logger.LogWarning("PAYMENT_FAILED: {LogMessage}", logMessage);
                        break;
                    case PaymentLogStatus.Initiated:
                        _logger.LogInformation("PAYMENT_INITIATED: {LogMessage}", logMessage);
                        break;
                    case PaymentLogStatus.Cancelled:
                        _logger.LogWarning("PAYMENT_CANCELLED: {LogMessage}", logMessage);
                        break;
                    case PaymentLogStatus.Pending:
                        _logger.LogInformation("PAYMENT_PENDING: {LogMessage}", logMessage);
                        break;
                    default:
                        _logger.LogInformation("PAYMENT_LOG: {LogMessage}", logMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Loglama hatası durumunda console'a yaz
                _logger.LogError(ex, "Failed to log payment attempt to database");
                Console.WriteLine($"PAYMENT_LOG_ERROR: {ex.Message}");
            }
        }

        // Ödeme başlatma log kaydı
        private async Task<PaymentLogEntry> CreateInitiateLog(PaymentInitiateRequest request, string userId, string userRole)
        {
            var logEntry = new PaymentLogEntry
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid().ToString(),
                CartId = request.CartId, // CartId string olarak set ediyoruz
                UserId = userId,
                UserRole = userRole,
                PaymentMethod = request.PaymentMethod,
                Amount = request.TotalAmount,
                Status = PaymentLogStatus.Initiated,
                Timestamp = DateTime.UtcNow,
                RequestData = JsonSerializer.Serialize(request),
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CustomerPhone = request.CustomerPhone,
                Notes = request.Notes,
                TseRequired = request.TseRequired,
                TaxNumber = request.TaxNumber,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ErrorDetails = null,
                ResponseData = null,
                ProcessingTimeMs = null,
                TseSignature = null,
                InvoiceId = null,
                ReceiptNumber = null
            };

            await LogPaymentAttempt(logEntry);
            return logEntry;
        }

        // Ödeme tamamlama log kaydı
        private async Task<PaymentLogEntry> CreateConfirmLog(string paymentSessionId, PaymentConfirmRequest request, 
            string userId, string userRole, PaymentLogStatus status, string errorDetails = null, 
            PaymentConfirmResponse response = null, double? processingTimeMs = null)
        {
            var logEntry = new PaymentLogEntry
            {
                Id = Guid.NewGuid(),
                SessionId = paymentSessionId,
                CartId = null, // Session'dan alınacak
                UserId = userId,
                UserRole = userRole,
                PaymentMethod = null, // Session'dan alınacak
                Amount = null, // Session'dan alınacak
                Status = status,
                Timestamp = DateTime.UtcNow,
                RequestData = JsonSerializer.Serialize(request),
                CustomerId = null, // Session'dan alınacak
                CustomerName = null, // Session'dan alınacak
                CustomerEmail = null, // Session'dan alınacak
                CustomerPhone = null, // Session'dan alınacak
                Notes = null, // Session'dan alınacak
                TseRequired = null, // Session'dan alınacak
                TaxNumber = null, // Session'dan alınacak
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                ErrorDetails = errorDetails,
                ResponseData = response != null ? JsonSerializer.Serialize(response) : null,
                ProcessingTimeMs = processingTimeMs,
                TseSignature = request.TseSignature,
                InvoiceId = response?.InvoiceId,
                ReceiptNumber = response?.ReceiptNumber
            };

            await LogPaymentAttempt(logEntry);
            return logEntry;
        }

        // Client IP adresini al
        private string GetClientIpAddress()
        {
            try
            {
                var forwardedHeader = Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHeader))
                {
                    return forwardedHeader.Split(',')[0].Trim();
                }

                var remoteIp = HttpContext.Connection.RemoteIpAddress;
                return remoteIp?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        // User Agent bilgisini al
        private string GetUserAgent()
        {
            try
            {
                return Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        // Ödeme işlemi performans metrikleri
        private async Task LogPaymentMetrics(string sessionId, double processingTimeMs, bool isSuccess)
        {
            try
            {
                var metrics = new PaymentMetrics
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    ProcessingTimeMs = processingTimeMs,
                    IsSuccess = isSuccess,
                    Timestamp = DateTime.UtcNow,
                    ServerLoad = Environment.ProcessorCount,
                    MemoryUsage = GC.GetTotalMemory(false)
                };

                _context.PaymentMetrics.Add(metrics);
                await _context.SaveChangesAsync();

                // Performans logları
                if (processingTimeMs > 5000) // 5 saniyeden uzun süren işlemler
                {
                    _logger.LogWarning("SLOW_PAYMENT_PROCESSING: Session {SessionId} took {ProcessingTime}ms", 
                        sessionId, processingTimeMs);
                }
                else if (processingTimeMs > 1000) // 1 saniyeden uzun süren işlemler
                {
                    _logger.LogInformation("PAYMENT_PROCESSING_TIME: Session {SessionId} took {ProcessingTime}ms", 
                        sessionId, processingTimeMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log payment metrics for session {SessionId}", sessionId);
            }
        }

        // GET: api/payment - Tüm ödemeleri getir (Sadece Admin ve Manager)
        [HttpGet]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetPayments()
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .Include(p => p.Customer)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/{id} - Belirli bir ödemeyi getir (Sadece Admin, Manager ve ödeme sahibi)
        [HttpGet("{id}")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<PaymentDetails>> GetPayment(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                var payment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                // Cashier sadece kendi ödemelerini görebilir
                if (userRole == "Cashier" && payment.UserId != userId)
                {
                    return Forbid();
                }

                return Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // POST: api/payment - Yeni ödeme oluştur (Sadece Admin ve Manager)
        [HttpPost]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<PaymentDetails>> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Invoice'ı kontrol et
                var invoice = await _context.Invoices.FindAsync(request.InvoiceId);
                if (invoice == null)
                {
                    return NotFound(new { message = "Invoice not found" });
                }

                // Ödeme tutarı kontrol et
                if (request.Amount <= 0)
                {
                    return BadRequest(new { message = "Payment amount must be greater than zero" });
                }

                // Ödeme yöntemi kontrol et
                if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
                {
                    return BadRequest(new { message = "Invalid payment method" });
                }

                var payment = new PaymentDetails
                {
                    InvoiceId = request.InvoiceId,
                    CustomerId = request.CustomerId,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Completed,
                    Reference = request.Reference,
                    Notes = request.Notes,
                    TransactionId = request.TransactionId
                };

                _context.PaymentDetails.Add(payment);

                // Invoice'ın ödenen tutarını güncelle
                invoice.PaidAmount += request.Amount;
                invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                invoice.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Oluşturulan payment'ı döndür
                var createdPayment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.Id == payment.Id);

                return CreatedAtAction(nameof(GetPayment), new { id = createdPayment.Id }, createdPayment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // PUT: api/payment/{id}/status - Ödeme durumunu güncelle (Sadece Admin ve Manager)
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> UpdatePaymentStatus(Guid id, [FromBody] UpdatePaymentStatusRequest request)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(id);
                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                payment.Status = request.Status;
                payment.UpdatedAt = DateTime.UtcNow;

                // Eğer ödeme iptal edildiyse, invoice'ı güncelle
                if (request.Status == PaymentStatus.Cancelled)
                {
                    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
                    if (invoice != null)
                    {
                        invoice.PaidAmount -= payment.Amount;
                        invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                        invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Payment status updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // DELETE: api/payment/{id} - Ödeme sil (Sadece Admin)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeletePayment(Guid id)
        {
            try
            {
                var payment = await _context.PaymentDetails.FindAsync(id);
                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                // Ödeme iptal edildiyse, invoice'ı güncelle
                if (payment.Status == PaymentStatus.Completed)
                {
                    var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
                    if (invoice != null)
                    {
                        invoice.PaidAmount -= payment.Amount;
                        invoice.RemainingAmount = invoice.TotalAmount - invoice.PaidAmount;
                        invoice.Status = invoice.RemainingAmount <= 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
                        invoice.UpdatedAt = DateTime.UtcNow;
                    }
                }

                _context.PaymentDetails.Remove(payment);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Payment deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment {PaymentId}", id);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/invoice/{invoiceId} - Invoice'a ait ödemeleri getir (Sadece Admin, Manager ve Cashier)
        [HttpGet("invoice/{invoiceId}")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetInvoicePayments(Guid invoiceId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                var payments = await _context.PaymentDetails
                    .Where(p => p.InvoiceId == invoiceId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                // Cashier sadece kendi ödemelerini görebilir
                if (userRole == "Cashier")
                {
                    payments = payments.Where(p => p.UserId == userId).ToList();
                }

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting invoice payments {InvoiceId}", invoiceId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/customer/{customerId} - Müşteriye ait ödemeleri getir (Sadece Admin ve Manager)
        [HttpGet("customer/{customerId}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetCustomerPayments(Guid customerId)
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Where(p => p.CustomerId == customerId)
                    .Include(p => p.Invoice)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer payments {CustomerId}", customerId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/method/{method} - Ödeme yöntemine göre ödemeleri getir (Sadece Admin ve Manager)
        [HttpGet("method/{method}")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<IEnumerable<PaymentDetails>>> GetPaymentsByMethod(PaymentMethod method)
        {
            try
            {
                var payments = await _context.PaymentDetails
                    .Where(p => p.PaymentMethod == method)
                    .Include(p => p.Invoice)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();

                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments by method {Method}", method);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // GET: api/payment/methods - Desteklenen ödeme yöntemlerini getir (Tüm authenticated kullanıcılar)
        [HttpGet("methods")]
        [Authorize(Roles = "Administrator,Manager,Cashier,Demo")]
        public async Task<ActionResult<PaymentMethodsResponse>> GetPaymentMethods()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                _logger.LogInformation("Payment methods requested by user {UserId} with role {UserRole}", userId, userRole);

                // Kullanıcı rolüne göre ödeme yöntemlerini filtrele
                var availableMethods = new List<PaymentMethodInfo>();

                // Demo kullanıcılar sadece test ödeme yöntemlerini görebilir
                if (userRole == "Demo")
                {
                    availableMethods.AddRange(new[]
                    {
                        new PaymentMethodInfo
                        {
                            Method = PaymentMethod.Cash,
                            Name = "Cash (Demo)",
                            Description = "Only for demo purposes",
                            IsEnabled = true,
                            RequiresTse = false,
                            Icon = "cash-icon",
                            MinAmount = 0.01m,
                            MaxAmount = 1000.00m
                        },
                        new PaymentMethodInfo
                        {
                            Method = PaymentMethod.Voucher,
                            Name = "Voucher (Demo)",
                            Description = "Only for demo purposes",
                            IsEnabled = true,
                            RequiresTse = false,
                            Icon = "voucher-icon",
                            MinAmount = 0.01m,
                            MaxAmount = 500.00m
                        }
                    });
                }
                else
                {
                    // Gerçek kullanıcılar tüm ödeme yöntemlerini görebilir
                    availableMethods.AddRange(new[]
                    {
                        new PaymentMethodInfo
                        {
                            Method = PaymentMethod.Cash,
                            Name = "Cash",
                            Description = "Payment at the cash register",
                            IsEnabled = true,
                            RequiresTse = true,
                            Icon = "cash-icon",
                            MinAmount = 0.01m,
                            MaxAmount = 10000.00m
                        },
                        new PaymentMethodInfo
                        {
                            Method = PaymentMethod.Card,
                            Name = "Card Payment",
                            Description = "EC-Card, Credit Card, etc.",
                            IsEnabled = true,
                            RequiresTse = true,
                            Icon = "card-icon",
                            MinAmount = 0.01m,
                            MaxAmount = 50000.00m
                        },
                        new PaymentMethodInfo
                        {
                            Method = PaymentMethod.Voucher,
                            Name = "Voucher",
                            Description = "Gift Voucher or Discount Voucher",
                            IsEnabled = true,
                            RequiresTse = false,
                            Icon = "voucher-icon",
                            MinAmount = 0.01m,
                            MaxAmount = 1000.00m
                        }
                    });

                    // TSE cihazı bağlı mı kontrol et
                    var tseStatus = await CheckTseStatus();
                    if (!tseStatus.IsConnected)
                    {
                        // TSE bağlı değilse, TSE gerektiren yöntemleri devre dışı bırak
                        foreach (var method in availableMethods.Where(m => m.RequiresTse))
                        {
                            method.IsEnabled = false;
                            method.Description += " (TSE not connected)";
                        }
                    }
                }

                _logger.LogInformation("Returning {Count} payment methods for user {UserId}", availableMethods.Count, userId);

                return Ok(new PaymentMethodsResponse
                {
                    Success = true,
                    Methods = availableMethods,
                    TseStatus = await CheckTseStatus(),
                    Message = "Payment methods retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment methods");
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving payment methods",
                    error = ex.Message 
                });
            }
        }

        // POST: api/payment/print-receipt - Makbuz yazdır (Tüm authenticated kullanıcılar)
        [HttpPost("print-receipt")]
        [Authorize(Roles = "Administrator,Manager,Cashier,Demo")]
        public async Task<ActionResult<ReceiptPrintResponse>> PrintReceipt([FromBody] ReceiptPrintRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

                _logger.LogInformation("Receipt print requested by user {UserId} for payment {PaymentId}", userId, request.PaymentId);

                // Payment'ı bul
                var payment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .FirstOrDefaultAsync(p => p.Id == request.PaymentId);

                if (payment == null)
                {
                    return NotFound(new { message = "Payment not found" });
                }

                // Cart'i bul
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(c => c.CartId == request.CartId);

                if (cart == null)
                {
                    return NotFound(new { message = "Cart not found" });
                }

                // Makbuz oluştur
                var receiptContent = await _receiptService.GenerateReceiptAsync(payment, payment.Invoice, cart);
                
                // Makbuzu yazdır
                var printSuccess = await _receiptService.PrintReceiptAsync(receiptContent, request.PrinterName);
                
                if (printSuccess)
                {
                    _logger.LogInformation("Receipt printed successfully for payment {PaymentId} by user {UserId}", request.PaymentId, userId);
                    
                    return Ok(new ReceiptPrintResponse
                    {
                        Success = true,
                        Message = "Receipt printed successfully",
                        ReceiptContent = receiptContent,
                        PrintedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Yazdırma başarısız, dosyaya kaydet
                    var fileName = $"receipt_{GenerateReceiptNumber()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
                    await _receiptService.SaveReceiptToFileAsync(receiptContent, fileName);
                    
                    _logger.LogWarning("Receipt printing failed for payment {PaymentId}, saved to file: {FileName}", request.PaymentId, fileName);
                    
                    return Ok(new ReceiptPrintResponse
                    {
                        Success = false,
                        Message = "Receipt printing failed, saved to file instead",
                        ReceiptContent = receiptContent,
                        SavedToFile = fileName,
                        PrintedAt = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt for payment {PaymentId}", request?.PaymentId);
                return StatusCode(500, new { 
                    message = "Internal server error during receipt printing",
                    error = ex.Message 
                });
            }
        }

        // GET: api/payment/printers - Mevcut yazıcıları listele (Tüm authenticated kullanıcılar)
        [HttpGet("printers")]
        [Authorize(Roles = "Administrator,Manager,Cashier,Demo")]
        public ActionResult<PrintersResponse> GetAvailablePrinters()
        {
            try
            {
                var printers = _receiptService.GetAvailablePrinters();
                
                return Ok(new PrintersResponse
                {
                    Success = true,
                    Printers = printers,
                    Message = "Available printers retrieved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available printers");
                return StatusCode(500, new { 
                    message = "Internal server error while retrieving printers",
                    error = ex.Message 
                });
            }
        }

        // POST: api/payment/refund - Ödeme iadesi (Sadece Admin ve Manager)
        [HttpPost("refund")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<PaymentRefundResponse>> RefundPayment([FromBody] PaymentRefundRequest request)
        {
            var startTime = DateTime.UtcNow;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            try
            {
                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    
                    _logger.LogWarning("Invalid model state for payment refund request by user {UserId}: {Errors}", 
                        userId, validationErrors);
                    
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                _logger.LogInformation("Payment refund requested by user {UserId} for payment {PaymentId} with amount {RefundAmount}", 
                    userId, request.PaymentId, request.RefundAmount);

                // Payment'ı bul ve kontrol et
                var payment = await _context.PaymentDetails
                    .Include(p => p.Invoice)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == request.PaymentId);

                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found for refund request by user {UserId}", 
                        request.PaymentId, userId);
                    
                    return NotFound(new { message = "Payment not found" });
                }

                // Ödeme durumunu kontrol et
                if (payment.Status != PaymentStatus.Completed)
                {
                    _logger.LogWarning("Payment {PaymentId} is not completed and cannot be refunded. Status: {Status}, User: {UserId}", 
                        request.PaymentId, payment.Status, userId);
                    
                    return BadRequest(new { message = "Only completed payments can be refunded" });
                }

                // İade tutarını kontrol et
                if (request.RefundAmount <= 0)
                {
                    _logger.LogWarning("Invalid refund amount {RefundAmount} for payment {PaymentId} by user {UserId}", 
                        request.RefundAmount, request.PaymentId, userId);
                    
                    return BadRequest(new { message = "Refund amount must be greater than zero" });
                }

                if (request.RefundAmount > payment.Amount)
                {
                    _logger.LogWarning("Refund amount {RefundAmount} exceeds payment amount {PaymentAmount} for payment {PaymentId} by user {UserId}", 
                        request.RefundAmount, payment.Amount, request.PaymentId, userId);
                    
                    return BadRequest(new { 
                        message = "Refund amount cannot exceed the original payment amount",
                        maxRefundAmount = payment.Amount
                    });
                }

                // Daha önce iade yapılmış mı kontrol et
                var existingRefunds = await _context.PaymentDetails
                    .Where(p => p.OriginalPaymentId == request.PaymentId && p.Status == PaymentStatus.Refunded)
                    .SumAsync(p => p.Amount);

                var totalRefunded = existingRefunds + request.RefundAmount;
                if (totalRefunded > payment.Amount)
                {
                    _logger.LogWarning("Total refund amount {TotalRefunded} would exceed payment amount {PaymentAmount} for payment {PaymentId} by user {UserId}", 
                        totalRefunded, payment.Amount, request.PaymentId, userId);
                    
                    return BadRequest(new { 
                        message = "Total refund amount would exceed the original payment amount",
                        alreadyRefunded = existingRefunds,
                        maxRefundAmount = payment.Amount - existingRefunds
                    });
                }

                // İade sebebini kontrol et
                if (string.IsNullOrWhiteSpace(request.RefundReason))
                {
                    _logger.LogWarning("Refund reason is required for payment {PaymentId} by user {UserId}", 
                        request.PaymentId, userId);
                    
                    return BadRequest(new { message = "Refund reason is required" });
                }

                // İade tutarı ile orijinal ödeme tutarı arasında tutarlılık kontrolü
                var originalPaymentAmount = Math.Abs(payment.Amount); // Negatif değerler için mutlak değer
                var refundAmount = request.RefundAmount;
                var tolerance = 0.01m; // 1 cent tolerans
                
                // İade tutarının orijinal ödeme tutarına uygun olup olmadığını kontrol et
                if (Math.Abs(refundAmount - originalPaymentAmount) > tolerance && refundAmount != originalPaymentAmount)
                {
                    _logger.LogWarning("Refund amount consistency check failed for payment {PaymentId}. Original amount: {OriginalAmount}, Refund amount: {RefundAmount}, User: {UserId}", 
                        request.PaymentId, originalPaymentAmount, refundAmount, userId);
                    
                    return BadRequest(new { 
                        message = "Refund amount must match the original payment amount for full refunds",
                        originalPaymentAmount = originalPaymentAmount,
                        refundAmount = refundAmount,
                        difference = Math.Abs(refundAmount - originalPaymentAmount),
                        note = "For partial refunds, ensure the refund amount is less than the original amount"
                    });
                }

                _logger.LogInformation("Refund amount validation passed for payment {PaymentId}. Original amount: {OriginalAmount}, Refund amount: {RefundAmount}", 
                    request.PaymentId, originalPaymentAmount, refundAmount);

                // İade işlemi için yeni PaymentDetails oluştur
                var refundPayment = new PaymentDetails
                {
                    InvoiceId = payment.InvoiceId,
                    CustomerId = payment.CustomerId,
                    UserId = userId,
                    Amount = -request.RefundAmount, // Negatif tutar iade olduğunu gösterir
                    PaymentMethod = payment.PaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Refunded,
                    Reference = $"REFUND-{request.PaymentId}",
                    Notes = request.RefundReason,
                    TransactionId = $"REF-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}",
                    OriginalPaymentId = request.PaymentId, // Orijinal ödeme referansı
                    RefundReason = request.RefundReason,
                    RefundMethod = request.RefundMethod.HasValue ? request.RefundMethod.Value : payment.PaymentMethod,
                    RefundedBy = userId,
                    RefundedAt = DateTime.UtcNow
                };

                _context.PaymentDetails.Add(refundPayment);

                // Orijinal ödeme durumunu güncelle
                if (totalRefunded >= payment.Amount)
                {
                    payment.Status = PaymentStatus.FullyRefunded;
                }
                else
                {
                    payment.Status = PaymentStatus.PartiallyRefunded;
                }
                payment.UpdatedAt = DateTime.UtcNow;

                // Invoice'ı güncelle
                if (payment.Invoice != null)
                {
                    payment.Invoice.PaidAmount -= request.RefundAmount;
                    payment.Invoice.RemainingAmount = payment.Invoice.TotalAmount - payment.Invoice.PaidAmount;
                    
                    if (payment.Invoice.PaidAmount <= 0)
                    {
                        payment.Invoice.Status = InvoiceStatus.Unpaid;
                    }
                    else if (payment.Invoice.PaidAmount < payment.Invoice.TotalAmount)
                    {
                        payment.Invoice.Status = InvoiceStatus.PartiallyPaid;
                    }
                    
                    payment.Invoice.UpdatedAt = DateTime.UtcNow;
                }

                // İade log kaydı oluştur
                var refundLogEntry = new PaymentLogEntry
                {
                    Id = Guid.NewGuid(),
                    SessionId = Guid.NewGuid().ToString(),
                    CartId = null,
                    UserId = userId,
                    UserRole = userRole,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = request.RefundAmount,
                    Status = PaymentLogStatus.Refunded,
                    Timestamp = DateTime.UtcNow,
                    RequestData = JsonSerializer.Serialize(request),
                    CustomerId = payment.CustomerId,
                    CustomerName = payment.Customer?.Name,
                    CustomerEmail = payment.Customer?.Email,
                    CustomerPhone = payment.Customer?.Phone,
                    Notes = request.RefundReason,
                    TseRequired = false,
                    TaxNumber = null,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = GetUserAgent(),
                    ErrorDetails = null,
                    ResponseData = JsonSerializer.Serialize(new { 
                        refundAmount = request.RefundAmount,
                        refundReason = request.RefundReason,
                        originalPaymentId = request.PaymentId
                    }),
                    ProcessingTimeMs = null,
                    TseSignature = null,
                    InvoiceId = payment.InvoiceId,
                    ReceiptNumber = null
                };

                await LogPaymentAttempt(refundLogEntry);

                // Veritabanı değişikliklerini kaydet
                await _context.SaveChangesAsync();

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // İade makbuzu oluştur ve yazdır
                try
                {
                    _logger.LogInformation("Generating refund receipt for payment {PaymentId}", payment.Id);
                    
                    // İade makbuzu için özel içerik oluştur
                    var refundReceiptContent = await GenerateRefundReceiptAsync(refundPayment, payment, request.RefundReason);
                    
                    // İade makbuzunu yazdır
                    var printSuccess = await _receiptService.PrintReceiptAsync(refundReceiptContent, request.PrinterName);
                    
                    if (printSuccess)
                    {
                        _logger.LogInformation("Refund receipt printed successfully for payment {PaymentId}", payment.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Refund receipt printing failed for payment {PaymentId}, saved to file instead", payment.Id);
                        
                        // Dosyaya kaydet
                        var fileName = $"refund_receipt_{payment.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
                        await _receiptService.SaveReceiptToFileAsync(refundReceiptContent, fileName);
                    }
                }
                catch (Exception receiptEx)
                {
                    _logger.LogError(receiptEx, "Error generating/printing refund receipt for payment {PaymentId}, but refund was successful", payment.Id);
                }

                // Response oluştur
                var response = new PaymentRefundResponse
                {
                    Success = true,
                    RefundId = refundPayment.Id,
                    OriginalPaymentId = request.PaymentId,
                    RefundAmount = request.RefundAmount,
                    RefundReason = request.RefundReason,
                    RefundMethod = refundPayment.RefundMethod ?? PaymentMethod.Cash,
                    RefundedBy = userId,
                    RefundedAt = refundPayment.RefundedAt ?? DateTime.UtcNow,
                    RemainingAmount = payment.Amount - totalRefunded,
                    Message = "Payment refunded successfully"
                };

                // Audit logging for successful payment refund
                await _auditLogService.LogPaymentOperationAsync(
                    AuditLogActions.PAYMENT_REFUND,
                    AuditLogEntityTypes.PAYMENT,
                    refundPayment.Id,
                    userId,
                    userRole,
                    request.RefundAmount,
                    refundPayment.RefundMethod?.ToString(),
                    null,
                    refundPayment.TransactionId,
                    refundPayment.TransactionId, // Use transactionId as correlationId
                    request,
                    response,
                    $"Payment refunded for payment {request.PaymentId}",
                    request.RefundReason,
                    AuditLogStatus.Success,
                    null,
                    processingTime);

                // Performans metrikleri
                await LogPaymentMetrics(refundLogEntry.SessionId, processingTime, true);

                _logger.LogInformation("Payment refund completed successfully for payment {PaymentId} by user {UserId}. Refund amount: {RefundAmount}, Processing time: {ProcessingTime}ms", 
                    request.PaymentId, userId, request.RefundAmount, processingTime);

                return Ok(response);
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Error processing refund for payment {PaymentId} by user {UserId}. Processing time: {ProcessingTime}ms", 
                    request?.PaymentId, userId, processingTime);
                
                // Hata loglama
                var errorLogEntry = new PaymentLogEntry
                {
                    Id = Guid.NewGuid(),
                    SessionId = Guid.NewGuid().ToString(),
                    CartId = null,
                    UserId = userId,
                    UserRole = userRole,
                    PaymentMethod = PaymentMethod.Cash, // Default value instead of Unknown
                    Amount = request?.RefundAmount ?? 0,
                    Status = PaymentLogStatus.Failed,
                    Timestamp = DateTime.UtcNow,
                    RequestData = request != null ? JsonSerializer.Serialize(request) : null,
                    CustomerId = null,
                    CustomerName = null,
                    CustomerEmail = null,
                    CustomerPhone = null,
                    Notes = "Refund processing failed",
                    TseRequired = false,
                    TaxNumber = null,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = GetUserAgent(),
                    ErrorDetails = ex.Message,
                    ResponseData = null,
                    ProcessingTimeMs = processingTime,
                    TseSignature = null,
                    InvoiceId = null,
                    ReceiptNumber = null
                };

                await LogPaymentAttempt(errorLogEntry);
                
                // Performans metrikleri
                await LogPaymentMetrics(errorLogEntry.SessionId, processingTime, false);
                
                return StatusCode(500, new { 
                    message = "Internal server error during payment refund",
                    error = ex.Message 
                });
            }
        }

        // POST: api/payment/cancel - Ödeme işlemini iptal et (Kasiyer ve üstü)
        [HttpPost("cancel")]
        [Authorize(Roles = "Administrator,Manager,Cashier")]
        public async Task<ActionResult<PaymentCancelResponse>> CancelPayment([FromBody] PaymentCancelRequest request)
        {
            var startTime = DateTime.UtcNow;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            try
            {
                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    
                    _logger.LogWarning("Invalid model state for payment cancellation request by user {UserId}: {Errors}", 
                        userId, validationErrors);
                    
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                _logger.LogInformation("Payment cancellation requested by user {UserId} for session {SessionId}", 
                    userId, request.PaymentSessionId);

                // Ödeme oturumunu kontrol et
                var paymentSession = await _context.PaymentSessions
                    .Include(ps => ps.Cart)
                    .ThenInclude(c => c.Items)
                    .FirstOrDefaultAsync(ps => ps.SessionId == request.PaymentSessionId);

                if (paymentSession == null)
                {
                    _logger.LogWarning("Payment session {SessionId} not found for cancellation by user {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    return NotFound(new { message = "Payment session not found" });
                }

                // Session'ın durumunu kontrol et
                if (paymentSession.Status == PaymentSessionStatus.Cancelled)
                {
                    _logger.LogWarning("Payment session {SessionId} is already cancelled by user {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    return BadRequest(new { message = "Payment session is already cancelled" });
                }

                if (paymentSession.Status == PaymentSessionStatus.Completed)
                {
                    _logger.LogWarning("Cannot cancel completed payment session {SessionId} by user {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    return BadRequest(new { message = "Cannot cancel completed payment session" });
                }

                // Ödeme iptal log kaydı oluştur
                var cancelLogEntry = new PaymentLogEntry
                {
                    Id = Guid.NewGuid(),
                    SessionId = request.PaymentSessionId,
                    CartId = paymentSession.CartId,
                    UserId = userId,
                    UserRole = userRole,
                    PaymentMethod = paymentSession.PaymentMethod,
                    Amount = paymentSession.TotalAmount,
                    Status = PaymentLogStatus.Cancelled,
                    Timestamp = DateTime.UtcNow,
                    RequestData = JsonSerializer.Serialize(request),
                    CustomerId = paymentSession.CustomerId,
                    CustomerName = paymentSession.CustomerName,
                    CustomerEmail = paymentSession.CustomerEmail,
                    CustomerPhone = paymentSession.CustomerPhone,
                    Notes = request.CancellationReason,
                    TseRequired = paymentSession.TseRequired,
                    TaxNumber = paymentSession.TaxNumber,
                    IpAddress = GetClientIpAddress(),
                    UserAgent = GetUserAgent(),
                    ProcessingTimeMs = 0, // Will be updated after processing
                    ErrorDetails = null,
                    TseSignature = null,
                    ReceiptNumber = null,
                    InvoiceId = null
                };

                // Session'ı iptal et
                paymentSession.Status = PaymentSessionStatus.Cancelled;
                paymentSession.CancelledAt = DateTime.UtcNow;
                paymentSession.CancelledBy = userId;
                paymentSession.CancellationReason = request.CancellationReason;
                paymentSession.UpdatedAt = DateTime.UtcNow;

                // Sepeti temizle (ürünleri geri ekle)
                if (paymentSession.Cart != null)
                {
                    foreach (var item in paymentSession.Cart.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                // Veritabanına kaydet
                await _context.SaveChangesAsync();

                // Log kaydını ekle
                await LogPaymentAttempt(cancelLogEntry);

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // Audit logging for payment cancellation
                await _auditLogService.LogPaymentOperationAsync(
                    AuditLogActions.PAYMENT_CANCEL,
                    AuditLogEntityTypes.PAYMENT_SESSION,
                    null,
                    userId,
                    userRole,
                    paymentSession.TotalAmount,
                    paymentSession.PaymentMethod.ToString(),
                    null,
                    request.PaymentSessionId,
                    request.PaymentSessionId, // Use sessionId as correlationId
                    request,
                    new { 
                        sessionId = request.PaymentSessionId, 
                        cartId = paymentSession.CartId, 
                        totalAmount = paymentSession.TotalAmount,
                        cancellationReason = request.CancellationReason
                    },
                    $"Payment session cancelled for cart {paymentSession.CartId}",
                    null,
                    AuditLogStatus.Success,
                    null,
                    (double)processingTime);

                _logger.LogInformation("Payment session {SessionId} cancelled successfully by user {UserId}. Reason: {Reason}", 
                    request.PaymentSessionId, userId, request.CancellationReason);

                return Ok(new PaymentCancelResponse
                {
                    Success = true,
                    PaymentSessionId = request.PaymentSessionId,
                    CartId = paymentSession.CartId,
                    CancelledAt = paymentSession.CancelledAt.Value,
                    CancelledBy = userId,
                    CancellationReason = request.CancellationReason,
                    Message = "Payment session cancelled successfully"
                });
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Error cancelling payment session {SessionId} by user {UserId}. Processing time: {ProcessingTime}ms", 
                    request?.PaymentSessionId, userId, processingTime);
                
                // Hata loglama
                if (!string.IsNullOrEmpty(request?.PaymentSessionId))
                {
                    var errorLogEntry = new PaymentLogEntry
                    {
                        Id = Guid.NewGuid(),
                        SessionId = request.PaymentSessionId,
                        CartId = null,
                        UserId = userId,
                        UserRole = userRole,
                        PaymentMethod = PaymentMethod.Cash, // Default
                        Amount = 0,
                        Status = PaymentLogStatus.Failed,
                        Timestamp = DateTime.UtcNow,
                        RequestData = JsonSerializer.Serialize(request),
                        CustomerId = null,
                        CustomerName = null,
                        CustomerEmail = null,
                        CustomerPhone = null,
                        Notes = "Payment cancellation failed",
                        TseRequired = false,
                        TaxNumber = null,
                        IpAddress = GetClientIpAddress(),
                        UserAgent = GetUserAgent(),
                        ProcessingTimeMs = (double)processingTime,
                        ErrorDetails = ex.Message,
                        TseSignature = null,
                        ReceiptNumber = null,
                        InvoiceId = null
                    };
                    
                    await LogPaymentAttempt(errorLogEntry);
                }
                
                return StatusCode(500, new { 
                    message = "Internal server error during payment cancellation",
                    error = ex.Message 
                });
            }
        }

        // İade makbuzu oluştur
        private async Task<string> GenerateRefundReceiptAsync(PaymentDetails refundPayment, PaymentDetails originalPayment, string refundReason)
        {
            try
            {
                var receiptBuilder = new StringBuilder();

                // İade makbuzu başlığı
                receiptBuilder.AppendLine("=".PadRight(40, '='));
                receiptBuilder.AppendLine("         REFUND RECEIPT");
                receiptBuilder.AppendLine("=".PadRight(40, '='));
                receiptBuilder.AppendLine();

                // Şirket bilgileri
                receiptBuilder.AppendLine("Company: Demo Company");
                receiptBuilder.AppendLine("Address: Demo Address");
                receiptBuilder.AppendLine("Tax Number: ATU12345678");
                receiptBuilder.AppendLine("Cash Register ID: DEMO-KASSE-001");
                receiptBuilder.AppendLine();

                // İade detayları
                receiptBuilder.AppendLine($"Refund Receipt Number: {GenerateReceiptNumber()}");
                receiptBuilder.AppendLine($"Date: {DateTime.UtcNow:dd.MM.yyyy}");
                receiptBuilder.AppendLine($"Time: {DateTime.UtcNow:HH:mm:ss}");
                receiptBuilder.AppendLine($"Processed By: {refundPayment.RefundedBy}");
                receiptBuilder.AppendLine();

                // Orijinal ödeme bilgileri
                receiptBuilder.AppendLine("Original Payment Information:");
                receiptBuilder.AppendLine($"Payment ID: {originalPayment.Id}");
                receiptBuilder.AppendLine($"Payment Date: {originalPayment.PaymentDate:dd.MM.yyyy}");
                receiptBuilder.AppendLine($"Payment Method: {originalPayment.PaymentMethod}");
                receiptBuilder.AppendLine($"Original Amount: €{originalPayment.Amount:F2}");
                receiptBuilder.AppendLine();

                // İade bilgileri
                receiptBuilder.AppendLine("Refund Information:");
                receiptBuilder.AppendLine($"Refund Amount: €{Math.Abs(refundPayment.Amount):F2}");
                receiptBuilder.AppendLine($"Refund Method: {refundPayment.RefundMethod}");
                receiptBuilder.AppendLine($"Refund Reason: {refundReason}");
                receiptBuilder.AppendLine($"Refund Date: {refundPayment.RefundedAt:dd.MM.yyyy HH:mm:ss}");
                receiptBuilder.AppendLine();

                // Müşteri bilgileri (varsa)
                if (originalPayment.Customer != null)
                {
                    receiptBuilder.AppendLine("Customer Information:");
                    receiptBuilder.AppendLine($"Name: {originalPayment.Customer.Name}");
                    if (!string.IsNullOrEmpty(originalPayment.Customer.Email))
                        receiptBuilder.AppendLine($"Email: {originalPayment.Customer.Email}");
                    if (!string.IsNullOrEmpty(originalPayment.Customer.Phone))
                        receiptBuilder.AppendLine($"Phone: {originalPayment.Customer.Phone}");
                    receiptBuilder.AppendLine();
                }

                // Footer
                receiptBuilder.AppendLine("This is a refund receipt for the original payment.");
                receiptBuilder.AppendLine("Thank you for your business!");
                receiptBuilder.AppendLine("=".PadRight(40, '='));

                return receiptBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refund receipt for payment {PaymentId}", originalPayment.Id);
                return "Error generating refund receipt";
            }
        }

        // POST: api/payment/initiate - Ödeme işlemini başlat (Tüm authenticated kullanıcılar)
        [HttpPost("initiate")]
        [Authorize(Roles = "Administrator,Manager,Cashier,Demo")]
        public async Task<ActionResult<PaymentInitiateResponse>> InitiatePayment([FromBody] PaymentInitiateRequest request)
        {
            var startTime = DateTime.UtcNow;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            var sessionId = string.Empty;

            try
            {
                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    
                    _logger.LogWarning("Invalid model state for payment initiation request by user {UserId}: {Errors}", 
                        userId, validationErrors);
                    
                    // Loglama hatası
                    await CreateInitiateLog(request, userId, userRole);
                    
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                _logger.LogInformation("Payment initiation requested by user {UserId} with role {UserRole} for cart {CartId}", 
                    userId, userRole, request.CartId);

                // Sepeti kontrol et
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(c => c.CartId == request.CartId);

                if (cart == null)
                {
                    _logger.LogWarning("Cart {CartId} not found for payment initiation by user {UserId}", 
                        request.CartId, userId);
                    
                    // Loglama hatası
                    await CreateInitiateLog(request, userId, userRole);
                    
                    return NotFound(new { message = "Cart not found" });
                }

                // Sepet durumunu kontrol et
                if (cart.Status != CartStatus.Active)
                {
                    _logger.LogWarning("Cart {CartId} is not active for payment. Status: {Status}, User: {UserId}", 
                        cart.CartId, cart.Status, userId);
                    
                    // Loglama hatası
                    await CreateInitiateLog(request, userId, userRole);
                    
                    return BadRequest(new { message = "Cart is not active for payment" });
                }

                // Sepet boş mu kontrol et
                if (!cart.Items.Any())
                {
                    _logger.LogWarning("Cart {CartId} is empty and cannot be processed for payment by user {UserId}", 
                        cart.CartId, userId);
                    
                    // Loglama hatası
                    await CreateInitiateLog(request, userId, userRole);
                    
                    return BadRequest(new { message = "Cart is empty" });
                }

                // Toplam tutarı hesapla ve doğrula (KDV dahil)
                var cartSubtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
                var taxAmount = cartSubtotal * 0.20m; // %20 KDV
                var expectedTotalWithTax = cartSubtotal + taxAmount;
                
                // Tutar toleransı (küçük farklar için - örneğin yuvarlama hataları)
                var tolerance = 0.01m; // 1 cent tolerans
                var amountDifference = Math.Abs(request.TotalAmount - expectedTotalWithTax);
                
                if (amountDifference > tolerance)
                {
                    _logger.LogWarning("Total amount mismatch for cart {CartId}. Expected: {ExpectedTotal}, Received: {ReceivedTotal}, Difference: {Difference}, User: {UserId}", 
                        cart.CartId, expectedTotalWithTax, request.TotalAmount, amountDifference, userId);
                    
                    // Loglama hatası
                    await CreateInitiateLog(request, userId, userRole);
                    
                    return BadRequest(new { 
                        message = "Total amount mismatch - amount does not match cart total with tax", 
                        expectedTotal = expectedTotalWithTax,
                        receivedTotal = request.TotalAmount,
                        difference = amountDifference,
                        cartSubtotal = cartSubtotal,
                        taxAmount = taxAmount,
                        taxRate = "20%"
                    });
                }

                _logger.LogInformation("Payment amount validation passed for cart {CartId}. Expected: {ExpectedTotal}, Received: {ReceivedTotal}", 
                    cart.CartId, expectedTotalWithTax, request.TotalAmount);

                // Ödeme işlemi için geçici ID oluştur
                sessionId = Guid.NewGuid().ToString();
                
                // Ödeme oturumunu kaydet
                var paymentSession = new PaymentSession
                {
                    SessionId = sessionId,
                    CartId = cart.CartId, // CartId string olarak kalacak
                    UserId = userId,
                    UserRole = userRole,
                    TotalAmount = request.TotalAmount,
                    PaymentMethod = request.PaymentMethod,
                    CustomerId = request.CustomerId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail,
                    CustomerPhone = request.CustomerPhone,
                    Notes = request.Notes,
                    TseRequired = request.TseRequired,
                    TaxNumber = request.TaxNumber,
                    Status = PaymentSessionStatus.Initiated,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30) // 30 dakika geçerli
                };

                _context.PaymentSessions.Add(paymentSession);
                await _context.SaveChangesAsync();

                // Başarılı loglama
                var logEntry = await CreateInitiateLog(request, userId, userRole);
                logEntry.SessionId = sessionId;

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                // Audit logging for successful payment initiation
                await _auditLogService.LogPaymentOperationAsync(
                    AuditLogActions.PAYMENT_INITIATE,
                    AuditLogEntityTypes.PAYMENT_SESSION,
                    null,
                    userId,
                    userRole,
                    request.TotalAmount,
                    request.PaymentMethod.ToString(),
                    null,
                    sessionId,
                    sessionId, // Use sessionId as correlationId
                    request,
                    new { sessionId, cartId = cart.CartId, totalAmount = request.TotalAmount },
                    $"Payment session initiated for cart {cart.CartId}",
                    null,
                    AuditLogStatus.Success,
                    null,
                    (double)processingTime);

                _logger.LogInformation("Payment session {SessionId} initiated successfully for cart {CartId} with total {TotalAmount} by user {UserId}", 
                    sessionId, cart.CartId, request.TotalAmount, userId);

                return Ok(new PaymentInitiateResponse
                {
                    Success = true,
                    PaymentSessionId = sessionId,
                    CartId = cart.CartId,
                    TotalAmount = request.TotalAmount,
                    ItemsCount = cart.Items.Count,
                    ExpiresAt = paymentSession.ExpiresAt,
                    Message = "Payment session initiated successfully"
                });
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Error initiating payment for cart {CartId} by user {UserId}. Processing time: {ProcessingTime}ms", 
                    request?.CartId, userId, processingTime);
                
                // Hata loglama
                if (!string.IsNullOrEmpty(sessionId))
                {
                    await CreateConfirmLog(sessionId, new PaymentConfirmRequest 
                    { 
                        PaymentSessionId = sessionId 
                    }, userId, userRole, PaymentLogStatus.Failed, ex.Message, null, processingTime);
                }
                
                // Performans metrikleri
                await LogPaymentMetrics(sessionId, processingTime, false);
                
                return StatusCode(500, new { 
                    message = "Internal server error during payment initiation",
                    error = ex.Message 
                });
            }
        }

        // POST: api/payment/confirm - Ödeme işlemini tamamla (Tüm authenticated kullanıcılar)
        [HttpPost("confirm")]
        [Authorize(Roles = "Administrator,Manager,Cashier,Demo")]
        public async Task<ActionResult<PaymentConfirmResponse>> ConfirmPayment([FromBody] PaymentConfirmRequest request)
        {
            var startTime = DateTime.UtcNow;
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";

            try
            {
                if (!ModelState.IsValid)
                {
                    var validationErrors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));
                    
                    _logger.LogWarning("Invalid model state for payment confirmation request by user {UserId}: {Errors}", 
                        userId, validationErrors);
                    
                    return BadRequest(new { message = "Invalid request data", errors = ModelState });
                }

                _logger.LogInformation("Payment confirmation requested by user {UserId} for session {SessionId}", 
                    userId, request.PaymentSessionId);

                // Ödeme oturumunu kontrol et
                var paymentSession = await _context.PaymentSessions
                    .Include(ps => ps.Cart)
                    .ThenInclude(c => c.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(ps => ps.SessionId == request.PaymentSessionId);

                if (paymentSession == null)
                {
                    _logger.LogWarning("Payment session {SessionId} not found for user {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    return NotFound(new { message = "Payment session not found" });
                }

                // Oturum durumunu kontrol et (sadece temel durum kontrolü)
                if (paymentSession.Status == PaymentSessionStatus.Completed)
                {
                    _logger.LogWarning("Payment session {SessionId} is already completed. User: {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    return BadRequest(new { message = "Payment session is already completed" });
                }

                // TSE imzası gerekli mi kontrol et
                if (paymentSession.TseRequired && string.IsNullOrEmpty(request.TseSignature))
                {
                    _logger.LogWarning("TSE signature required but not provided for session {SessionId} by user {UserId}", 
                        request.PaymentSessionId, userId);
                    
                    // TSE hatası loglama
                    await CreateConfirmLog(request.PaymentSessionId, request, userId, userRole, 
                        PaymentLogStatus.Failed, "TSE signature is required for this payment");
                    
                    return BadRequest(new { message = "TSE signature is required for this payment" });
                }

                // Tutar bilgilerini logla (kontrol yapmadan)
                var cartTotal = paymentSession.Cart.Items.Sum(i => i.Quantity * i.UnitPrice);
                var taxAmount = cartTotal * 0.20m; // %20 KDV
                var expectedTotal = cartTotal + taxAmount;
                
                _logger.LogInformation("Processing payment for session {SessionId}. Cart total: {CartTotal}, Tax: {TaxAmount}, Expected: {ExpectedTotal}, Actual: {ActualTotal}", 
                    request.PaymentSessionId, cartTotal, taxAmount, expectedTotal, paymentSession.TotalAmount);

                // Invoice oluştur
                var invoice = new Invoice
                {
                    InvoiceNumber = GenerateInvoiceNumber(),
                    InvoiceDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    Status = InvoiceStatus.Paid,
                    Subtotal = paymentSession.Cart.Items.Sum(i => i.Quantity * i.UnitPrice),
                    TaxAmount = paymentSession.Cart.Items.Sum(i => i.Quantity * i.UnitPrice * 0.20m), // Varsayılan %20 KDV
                    TotalAmount = paymentSession.TotalAmount,
                    PaidAmount = paymentSession.TotalAmount,
                    RemainingAmount = 0,
                    CustomerName = paymentSession.CustomerName,
                    CustomerEmail = paymentSession.CustomerEmail,
                    CustomerPhone = paymentSession.CustomerPhone,
                    CustomerTaxNumber = paymentSession.TaxNumber,
                    CompanyName = "Demo Company", // Company settings'den alınacak
                    CompanyTaxNumber = "ATU12345678", // Company settings'den alınacak
                    CompanyAddress = "Demo Address", // Company settings'den alınacak
                    TseSignature = request.TseSignature ?? "DEMO-SIGNATURE",
                    KassenId = "DEMO-KASSE-001", // Cash register settings'den alınacak
                    TseTimestamp = DateTime.UtcNow,
                    CashRegisterId = Guid.NewGuid(), // Mevcut cash register ID'si alınacak
                    PaymentMethod = paymentSession.PaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    TaxDetails = JsonDocument.Parse("{\"standard\": 20, \"reduced\": 10, \"special\": 13}")
                };

                _context.Invoices.Add(invoice);

                // Invoice items JSON olarak ekle
                var invoiceItemsJson = JsonSerializer.Serialize(paymentSession.Cart.Items.Select(i => new
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.Quantity * i.UnitPrice,
                    TaxType = "standard",
                    TaxRate = 20.0m,
                    Notes = i.Notes
                }));

                invoice.InvoiceItems = JsonDocument.Parse(invoiceItemsJson);

                // Payment details oluştur
                var paymentDetails = new PaymentDetails
                {
                    InvoiceId = invoice.Id,
                    CustomerId = paymentSession.CustomerId,
                    UserId = userId, // Kullanıcı ID'sini set et
                    Amount = paymentSession.TotalAmount,
                    PaymentMethod = paymentSession.PaymentMethod,
                    PaymentDate = DateTime.UtcNow,
                    Status = PaymentStatus.Completed,
                    Reference = request.TransactionReference,
                    Notes = paymentSession.Notes,
                    TransactionId = request.TransactionId
                };

                _context.PaymentDetails.Add(paymentDetails);

                // Sepet durumunu güncelle
                paymentSession.Cart.Status = CartStatus.Completed;

                // Ödeme oturumunu tamamla
                paymentSession.Status = PaymentSessionStatus.Completed;
                paymentSession.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var response = new PaymentConfirmResponse
                {
                    Success = true,
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    ReceiptNumber = GenerateReceiptNumber(),
                    TseSignature = request.TseSignature,
                    TotalAmount = paymentSession.TotalAmount,
                    PaymentMethod = paymentSession.PaymentMethod,
                    Message = "Payment completed successfully"
                };

                // Generate and print receipt after successful payment with enhanced printer integration
                try
                {
                    _logger.LogInformation("Generating receipt for successful payment {PaymentId}", paymentDetails.Id);
                    
                    // Check printer status before attempting to print
                    var printerStatus = await _receiptService.GetPrinterStatusAsync();
                    _logger.LogInformation("Printer status check result: {Status}", printerStatus);
                    
                    // Generate receipt content
                    var receiptContent = await _receiptService.GenerateReceiptAsync(paymentDetails, invoice, paymentSession.Cart);
                    
                    // Attempt to print receipt with enhanced error handling
                    bool printSuccess = false;
                    string printErrorMessage = null;
                    
                    if (printerStatus == PrinterStatus.Ready)
                    {
                        try
                        {
                            // Print receipt to default printer
                            printSuccess = await _receiptService.PrintReceiptAsync(receiptContent);
                            
                            if (printSuccess)
                            {
                                _logger.LogInformation("Receipt printed successfully for payment {PaymentId} to default printer", paymentDetails.Id);
                                
                                // Log successful printing for audit
                                await _auditLogService.LogPaymentOperationAsync(
                                    AuditLogActions.RECEIPT_PRINTED,
                                    AuditLogEntityTypes.RECEIPT,
                                    paymentDetails.Id,
                                    userId,
                                    userRole,
                                    paymentSession.TotalAmount,
                                    "PRINT_SUCCESS",
                                    null,
                                    null,
                                    request.PaymentSessionId,
                                    null,
                                    null,
                                    $"Receipt printed successfully for payment {paymentDetails.Id}",
                                    null,
                                    AuditLogStatus.Success,
                                    null,
                                    0);
                            }
                            else
                            {
                                printErrorMessage = "Print command failed";
                                _logger.LogWarning("Receipt printing failed for payment {PaymentId}, attempting fallback", paymentDetails.Id);
                            }
                        }
                        catch (Exception printEx)
                        {
                            printErrorMessage = printEx.Message;
                            _logger.LogError(printEx, "Exception during receipt printing for payment {PaymentId}", paymentDetails.Id);
                        }
                    }
                    else
                    {
                        printErrorMessage = $"Printer not ready. Status: {printerStatus}";
                        _logger.LogWarning("Printer not ready for payment {PaymentId}. Status: {Status}", paymentDetails.Id, printerStatus);
                    }
                    
                    // Fallback: Save receipt to file if printing failed
                    if (!printSuccess)
                    {
                        try
                        {
                            var fileName = $"receipt_{GenerateReceiptNumber()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
                            await _receiptService.SaveReceiptToFileAsync(receiptContent, fileName);
                            
                            _logger.LogInformation("Receipt saved to file as fallback for payment {PaymentId}: {FileName}", 
                                paymentDetails.Id, fileName);
                            
                            // Log fallback action
                            await _auditLogService.LogPaymentOperationAsync(
                                AuditLogActions.RECEIPT_SAVED,
                                AuditLogEntityTypes.RECEIPT,
                                paymentDetails.Id,
                                userId,
                                userRole,
                                paymentSession.TotalAmount,
                                "FILE_SAVE",
                                null,
                                null,
                                request.PaymentSessionId,
                                null,
                                null,
                                $"Receipt saved to file due to printing failure: {printErrorMessage}",
                                null,
                                AuditLogStatus.Warning,
                                null,
                                0);
                        }
                        catch (Exception saveEx)
                        {
                            _logger.LogError(saveEx, "Failed to save receipt to file for payment {PaymentId}", paymentDetails.Id);
                        }
                    }
                    
                    // Generate digital receipt (HTML) for email or web display
                    try
                    {
                        var digitalReceipt = await _receiptService.GenerateDigitalReceiptAsync(paymentDetails, invoice, paymentSession.Cart);
                        
                        // Save digital receipt to file
                        var htmlFileName = $"receipt_{GenerateReceiptNumber()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
                        await _receiptService.SaveReceiptToFileAsync(digitalReceipt, htmlFileName);
                        
                        _logger.LogInformation("Digital receipt generated and saved for payment {PaymentId}: {FileName}", 
                            paymentDetails.Id, htmlFileName);
                    }
                    catch (Exception digitalEx)
                    {
                        _logger.LogError(digitalEx, "Failed to generate digital receipt for payment {PaymentId}", paymentDetails.Id);
                    }
                }
                catch (Exception receiptEx)
                {
                    // Log receipt generation error but don't fail the payment
                    _logger.LogError(receiptEx, "Error in receipt processing for payment {PaymentId}, but payment was successful", paymentDetails.Id);
                    
                    // Log receipt processing failure
                    await _auditLogService.LogPaymentOperationAsync(
                        AuditLogActions.RECEIPT_ERROR,
                        AuditLogEntityTypes.RECEIPT,
                        paymentDetails.Id,
                        userId,
                        userRole,
                        paymentSession.TotalAmount,
                        "RECEIPT_ERROR",
                        null,
                        null,
                        request.PaymentSessionId,
                        null,
                        null,
                        $"Receipt processing failed: {receiptEx.Message}",
                        null,
                        AuditLogStatus.Error,
                        null,
                        0);
                }

                // Audit logging for successful payment confirmation
                await _auditLogService.LogPaymentOperationAsync(
                    AuditLogActions.PAYMENT_CONFIRM,
                    AuditLogEntityTypes.PAYMENT,
                    paymentDetails.Id,
                    userId,
                    userRole,
                    paymentSession.TotalAmount,
                    paymentSession.PaymentMethod.ToString(),
                    request.TseSignature,
                    request.TransactionId,
                    request.PaymentSessionId, // Use sessionId as correlationId
                    request,
                    response,
                    $"Payment confirmed for session {request.PaymentSessionId}",
                    null,
                    AuditLogStatus.Success,
                    null,
                    (double)processingTime);

                // Başarılı loglama
                await CreateConfirmLog(request.PaymentSessionId, request, userId, userRole, 
                    PaymentLogStatus.Success, null, response, processingTime);

                // Performans metrikleri
                await LogPaymentMetrics(request.PaymentSessionId, processingTime, true);

                _logger.LogInformation("Payment completed successfully for session {SessionId} by user {UserId}. Invoice: {InvoiceId}, Amount: {Amount}, Processing time: {ProcessingTime}ms", 
                    request.PaymentSessionId, userId, invoice.Id, paymentSession.TotalAmount, processingTime);

                return Ok(response);
            }
            catch (Exception ex)
            {
                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogError(ex, "Error confirming payment for session {SessionId} by user {UserId}. Processing time: {ProcessingTime}ms", 
                    request?.PaymentSessionId, userId, processingTime);
                
                // Hata loglama
                await CreateConfirmLog(request.PaymentSessionId, request, userId, userRole, 
                    PaymentLogStatus.Failed, ex.Message, null, processingTime);
                
                // Performans metrikleri
                await LogPaymentMetrics(request.PaymentSessionId, processingTime, false);
                
                return StatusCode(500, new { 
                    message = "Internal server error during payment confirmation",
                    error = ex.Message 
                });
            }
        }

        // Yardımcı metodlar
        private string GenerateInvoiceNumber()
        {
            return $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }

        private string GenerateReceiptNumber()
        {
            return $"RCP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()}";
        }

        // TSE durumunu kontrol et
        private async Task<TseStatusInfo> CheckTseStatus()
        {
            try
            {
                // TSE cihazı bağlantı durumunu kontrol et
                // Bu kısım TSE entegrasyonu ile genişletilecek
                var isConnected = true; // Şimdilik true olarak varsayılıyor
                var lastCheck = DateTime.UtcNow;

                return new TseStatusInfo
                {
                    IsConnected = isConnected,
                    LastCheck = lastCheck,
                    DeviceInfo = isConnected ? "TSE Device Connected" : "TSE Device Not Connected"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking TSE status");
                return new TseStatusInfo
                {
                    IsConnected = false,
                    LastCheck = DateTime.UtcNow,
                    DeviceInfo = "TSE Status Check Failed"
                };
            }
        }
    }

    // DTOs
    public class CreatePaymentRequest
    {
        [Required]
        public Guid InvoiceId { get; set; }

        public Guid? CustomerId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }
    }

    public class UpdatePaymentStatusRequest
    {
        [Required]
        public PaymentStatus Status { get; set; }
    }

    public class PaymentInitiateRequest
    {
        [Required]
        public string CartId { get; set; } = string.Empty;

        [Required]
        public decimal TotalAmount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public Guid? CustomerId { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(255)]
        public string? CustomerEmail { get; set; }

        [MaxLength(20)]
        public string? CustomerPhone { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool TseRequired { get; set; }

        [MaxLength(50)]
        public string? TaxNumber { get; set; }
    }

    public class PaymentInitiateResponse
    {
        public bool Success { get; set; }
        public string PaymentSessionId { get; set; }
        public string CartId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int ItemsCount { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Message { get; set; }
    }

    public class PaymentConfirmRequest
    {
        [Required]
        public string PaymentSessionId { get; set; }

        [MaxLength(100)]
        public string? TransactionReference { get; set; }

        [MaxLength(100)]
        public string? TransactionId { get; set; }

        [MaxLength(500)]
        public string? TseSignature { get; set; }
    }

    public class PaymentConfirmResponse
    {
        public bool Success { get; set; }
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; }
        public string ReceiptNumber { get; set; }
        public string TseSignature { get; set; }
        public decimal TotalAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string Message { get; set; }
    }

    public class PaymentMethodsResponse
    {
        public bool Success { get; set; }
        public List<PaymentMethodInfo> Methods { get; set; }
        public TseStatusInfo TseStatus { get; set; }
        public string Message { get; set; }
    }

    public class PaymentMethodInfo
    {
        public PaymentMethod Method { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
        public bool RequiresTse { get; set; }
        public string Icon { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
    }

    public class TseStatusInfo
    {
        public bool IsConnected { get; set; }
        public DateTime LastCheck { get; set; }
        public string DeviceInfo { get; set; }
    }

    public class ReceiptPrintRequest
    {
        [Required]
        public Guid PaymentId { get; set; }
        [Required]
        public string CartId { get; set; } = string.Empty;
        public string? PrinterName { get; set; }
    }

    public class ReceiptPrintResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ReceiptContent { get; set; }
        public string? SavedToFile { get; set; }
        public DateTime PrintedAt { get; set; }
    }

    public class PrintersResponse
    {
        public bool Success { get; set; }
        public List<string> Printers { get; set; }
        public string Message { get; set; }
    }

    public class PaymentRefundRequest
    {
        [Required]
        public Guid PaymentId { get; set; }
        [Required]
        public decimal RefundAmount { get; set; }
        [Required]
        [MaxLength(500)]
        public string RefundReason { get; set; } = string.Empty;
        public PaymentMethod? RefundMethod { get; set; }
        public string? PrinterName { get; set; }
    }

    public class PaymentRefundResponse
    {
        public bool Success { get; set; }
        public Guid RefundId { get; set; }
        public Guid OriginalPaymentId { get; set; }
        public decimal RefundAmount { get; set; }
        public string RefundReason { get; set; }
        public PaymentMethod RefundMethod { get; set; }
        public string RefundedBy { get; set; }
        public DateTime RefundedAt { get; set; }
        public decimal RemainingAmount { get; set; }
        public string Message { get; set; }
    }

    public class PaymentCancelRequest
    {
        [Required]
        public string PaymentSessionId { get; set; }
        [MaxLength(500)]
        public string? CancellationReason { get; set; }
    }

    public class PaymentCancelResponse
    {
        public bool Success { get; set; }
        public string PaymentSessionId { get; set; }
        public string CartId { get; set; }
        public DateTime CancelledAt { get; set; }
        public string CancelledBy { get; set; }
        public string CancellationReason { get; set; }
        public string Message { get; set; }
    }
}
