using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// Ödeme işlemleri için controller - Service layer kullanarak
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PaymentController : BaseController
    {
        private readonly IPaymentService _paymentService;
        private readonly SignaturePipeline _signaturePipeline;

        public PaymentController(IPaymentService paymentService, SignaturePipeline signaturePipeline, ILogger<PaymentController> logger) 
            : base(logger)
        {
            _paymentService = paymentService;
            _signaturePipeline = signaturePipeline;
        }

        /// <summary>
        /// Mevcut ödeme yöntemlerini getir
        /// </summary>
        [HttpGet("methods")]
        [Authorize] // Authentication gerekli
        public IActionResult GetPaymentMethods()
        {
            try
            {
                // Kullanıcı ID'sini al
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var paymentMethods = new[]
                {
                    new { id = "cash", name = "Nakit", type = "cash", icon = "cash-outline" },
                    new { id = "card", name = "Kart", type = "card", icon = "card-outline" },
                    new { id = "voucher", name = "Kupon", type = "voucher", icon = "ticket-outline" },
                    new { id = "transfer", name = "Havale", type = "transfer", icon = "swap-horizontal-outline" }
                };

                return SuccessResponse(paymentMethods, "Payment methods retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Get Payment Methods");
            }
        }

        /// <summary>
        /// Yeni ödeme oluştur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                // Debug: Request'i logla
                _logger.LogInformation("CreatePayment called with request: {@Request}", request);
                
                // ModelState'i kontrol et
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    
                    _logger.LogWarning("ModelState validation failed: {Errors}", string.Join("; ", errors));
                    
                    return BadRequest(new
                    {
                        message = "Model validation failed",
                        errors = errors,
                        modelState = ModelState
                    });
                }
                
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    _logger.LogWarning("Base validation failed: {@ValidationResult}", validationResult);
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.CreatePaymentAsync(request, userId);
                
                if (result.Success)
                {
                    // Payment'tan TseSignature (JWS) çıkar - sadece admin/debug endpoint'te verilir
                    var paymentSafe = SanitizePaymentForResponse(result.Payment);
                    var responseData = new
                    {
                        success = true,
                        paymentId = result.PaymentId,
                        message = result.Message,
                        payment = paymentSafe,
                        tse = new
                        {
                            provider = result.TseProvider,
                            isDemoFiscal = result.IsDemoFiscal,
                            qrPayload = result.QrPayload,
                            receiptNumber = result.Payment?.ReceiptNumber
                        }
                    };
                    return CreatedAtAction(nameof(GetPayment), new { id = result.Payment!.Id }, 
                        responseData);
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create Payment");
            }
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPayment(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                return SuccessResponse(payment, "Payment retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payment with ID {id}");
            }
        }

        /// <summary>
        /// Müşteri ödemelerini getir
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetCustomerPayments(Guid customerId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetCustomerPaymentsAsync(customerId, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        customerId = customerId
                    }
                };

                return SuccessResponse(response, $"Retrieved payments for customer {customerId}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Customer Payments for customer {customerId}");
            }
        }

        /// <summary>
        /// Ödeme yöntemine göre ödemeleri getir
        /// </summary>
        [HttpGet("method/{paymentMethod}")]
        public async Task<IActionResult> GetPaymentsByMethod(string paymentMethod, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetPaymentsByMethodAsync(paymentMethod, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        paymentMethod = paymentMethod
                    }
                };

                return SuccessResponse(response, $"Retrieved payments by method {paymentMethod}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payments by Method {paymentMethod}");
            }
        }

        /// <summary>
        /// Tarih aralığına göre ödemeleri getir
        /// </summary>
        [HttpGet("date-range")]
        public async Task<IActionResult> GetPaymentsByDateRange(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate,
            [FromQuery] int pageNumber = 1, 
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (startDate > endDate)
                {
                    return ErrorResponse("Start date cannot be after end date", 400);
                }

                var (validPageNumber, validPageSize) = ValidatePagination(pageNumber, pageSize);
                
                var payments = await _paymentService.GetPaymentsByDateRangeAsync(startDate, endDate, validPageNumber, validPageSize);
                
                var response = new
                {
                    items = payments,
                    pagination = new
                    {
                        pageNumber = validPageNumber,
                        pageSize = validPageSize,
                        startDate = startDate,
                        endDate = endDate
                    }
                };

                return SuccessResponse(response, $"Retrieved payments from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payments by Date Range from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
        }

        /// <summary>
        /// Ödeme iptal et
        /// </summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelPayment(Guid id, [FromBody] CancelPaymentRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.CancelPaymentAsync(id, request.Reason, userId);
                
                if (result.Success)
                {
                    return SuccessResponse(result, "Payment cancelled successfully");
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Cancel Payment with ID {id}");
            }
        }

        /// <summary>
        /// Ödeme iade et
        /// </summary>
        [HttpPost("{id}/refund")]
        public async Task<IActionResult> RefundPayment(Guid id, [FromBody] RefundPaymentRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                var result = await _paymentService.RefundPaymentAsync(id, request.Amount, request.Reason, userId);
                
                if (result.Success)
                {
                    return SuccessResponse(result, "Refund processed successfully");
                }

                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Refund Payment with ID {id}");
            }
        }

        /// <summary>
        /// Ödeme istatistiklerini getir
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetPaymentStatistics(
            [FromQuery] string? startDate, 
            [FromQuery] string? endDate)
        {
            try
            {
                // 1. Validate inputs existence
                if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
                {
                    return ErrorResponse("StartDate and EndDate are required query parameters.", 400);
                }

                DateTime parsedStart;
                DateTime parsedEnd;

                // 2. Parse StartDate
                // Support "yyyy-MM-dd" explicitly for Date-only
                if (DateTime.TryParseExact(startDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dStart))
                {
                    parsedStart = dStart; // 00:00:00
                }
                else if (DateTime.TryParse(startDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out dStart))
                {
                    parsedStart = dStart;
                }
                else
                {
                    return ErrorResponse($"Invalid StartDate format: '{startDate}'. Supported: yyyy-MM-dd or ISO 8601.", 400);
                }

                // 3. Parse EndDate
                if (DateTime.TryParseExact(endDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dEnd))
                {
                    // Date-only input -> Set to End of Day (23:59:59.9999999)
                    parsedEnd = dEnd.Date.AddDays(1).AddTicks(-1);
                }
                else if (DateTime.TryParse(endDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out dEnd))
                {
                    parsedEnd = dEnd;
                }
                else
                {
                    return ErrorResponse($"Invalid EndDate format: '{endDate}'. Supported: yyyy-MM-dd or ISO 8601.", 400);
                }

                // 4. Validate Range
                if (parsedStart > parsedEnd)
                {
                    return ErrorResponse($"Start date ({parsedStart:yyyy-MM-dd}) cannot be after end date ({parsedEnd:yyyy-MM-dd})", 400);
                }

                // Ensure UTC if needed (assuming DB is UTC)
                // If Kind is Unspecified/Local, Convert to UTC or keep as is depending on system convention.
                // KasseAPI typically uses UTC for CreatedAt.
                if (parsedStart.Kind == DateTimeKind.Unspecified) parsedStart = DateTime.SpecifyKind(parsedStart, DateTimeKind.Utc);
                if (parsedEnd.Kind == DateTimeKind.Unspecified) parsedEnd = DateTime.SpecifyKind(parsedEnd, DateTimeKind.Utc);

                var statistics = await _paymentService.GetPaymentStatisticsAsync(parsedStart, parsedEnd);
                
                return SuccessResponse(statistics, $"Retrieved payment statistics from {parsedStart:yyyy-MM-dd HH:mm} to {parsedEnd:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payment Statistics from {startDate} to {endDate}");
            }
        }

        /// <summary>
        /// Get receipt data for payment
        /// </summary>
        [HttpGet("{id}/receipt")]
        public async Task<IActionResult> GetReceipt(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                var receiptData = await _paymentService.GetReceiptDataAsync(id);
                if (receiptData == null)
                {
                    return ErrorResponse($"Failed to generate receipt data for payment {id}", 500);
                }

                return SuccessResponse(receiptData, "Receipt data retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Receipt for Payment with ID {id}");
            }
        }

        /// <summary>
        /// TSE imzası oluştur
        /// </summary>
        [HttpPost("{id}/tse-signature")]
        [Authorize(Roles = "Administrator,Kasiyer")]
        public async Task<IActionResult> GenerateTseSignature(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                var signature = await _paymentService.GenerateTseSignatureAsync(payment);
                
                return SuccessResponse(new { tseSignature = signature }, "TSE signature generated successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Generate TSE Signature for Payment with ID {id}");
            }
        }

        /// <summary>
        /// RKSV Checklist 1–5 diagnostik: Ödeme imzasının adım adım doğrulaması.
        /// Admin only.
        /// </summary>
        /// <param name="id">Payment ID</param>
        /// <returns>Checklist steps: CMC match, JWS format, Hash, Signature verify, Base64URL padding</returns>
        /// <response code="200">Diagnostic steps returned</response>
        /// <response code="404">Payment not found</response>
        [HttpGet("{id}/signature-debug")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(typeof(IReadOnlyList<SignatureDiagnosticStep>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSignatureDebug(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                var tseSignature = payment.TseSignature;
                if (string.IsNullOrWhiteSpace(tseSignature))
                {
                    return SuccessResponse(new[]
                    {
                        new SignatureDiagnosticStep(1, "CMC match", "FAIL", "No signature"),
                        new SignatureDiagnosticStep(2, "JWS format", "FAIL", "Empty TseSignature"),
                        new SignatureDiagnosticStep(3, "Hash", "FAIL", "N/A"),
                        new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "N/A"),
                        new SignatureDiagnosticStep(5, "Base64URL padding", "FAIL", "N/A")
                    }, "No signature on payment");
                }

                var steps = _signaturePipeline.VerifyDiagnostic(tseSignature);
                // Admin: JWS (compactJws) debug endpoint'te döner
                return SuccessResponse(new { steps, compactJws = tseSignature }, "Signature diagnostic completed");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Signature Debug for Payment {id}");
            }
        }

        /// <summary>
        /// Payment objesinden TseSignature (JWS) çıkarır - default response güvenliği.
        /// </summary>
        private static object? SanitizePaymentForResponse(Models.PaymentDetails? payment)
        {
            if (payment == null) return null;
            var json = JsonSerializer.Serialize(payment, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj) obj.Remove("tseSignature");
            return node;
        }

        /// <summary>
        /// Manuel JWS doğrulama. RKSV Checklist 1–5 sonuçları.
        /// Admin only.
        /// </summary>
        /// <param name="request">compactJws = COMPACT JWS string (header.payload.signature)</param>
        /// <returns>Structured diagnostic result</returns>
        /// <response code="200">Verify result with checklist steps</response>
        [HttpPost("verify-signature")]
        [Authorize(Roles = "Administrator")]
        [ProducesResponseType(typeof(VerifySignatureResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult VerifySignature([FromBody] VerifySignatureRequest request)
        {
            try
            {
                var validationResult = ValidateModel();
                if (validationResult != null)
                {
                    return validationResult;
                }

                var steps = _signaturePipeline.VerifyDiagnostic(request.CompactJws ?? string.Empty);
                var allPass = steps.All(s => s.Status == "PASS");
                var response = new VerifySignatureResponse
                {
                    Valid = allPass,
                    Steps = steps,
                    Summary = allPass ? "All checklist items PASS" : $"Failed: {steps.Count(s => s.Status == "FAIL")} step(s)"
                };
                return SuccessResponse(response, "Verify completed");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Verify Signature");
            }
        }
    }

    /// <summary>
    /// Ödeme iptal request modeli
    /// </summary>
    public class CancelPaymentRequest
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ödeme iade request modeli
    /// </summary>
    public class RefundPaymentRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Manuel JWS doğrulama request.
    /// Swagger örnek: { "compactJws": "eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJrYXNzZW5JZCI6IktBU1NFLTAwMSJ9.signature..." }
    /// </summary>
    public class VerifySignatureRequest
    {
        /// <summary>
        /// COMPACT JWS string: base64url(header).base64url(payload).base64url(signature)
        /// </summary>
        [Required(ErrorMessage = "compactJws is required")]
        public string? CompactJws { get; set; }
    }

    /// <summary>
    /// JWS doğrulama yanıtı - Checklist 1–5 adımları.
    /// </summary>
    public class VerifySignatureResponse
    {
        public bool Valid { get; set; }
        public IReadOnlyList<SignatureDiagnosticStep> Steps { get; set; } = Array.Empty<SignatureDiagnosticStep>();
        public string Summary { get; set; } = string.Empty;
    }
}
