using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers
{
    /// <summary>
    /// POS payment handlers. Canonical route: <c>api/pos/payment/*</c>.
    /// Legacy alias <c>api/Payment/*</c> is the same actions (not a separate implementation) and emits
    /// deprecation headers via <see cref="LegacyRouteDeprecationFilter"/>.
    /// Do not add new features for the legacy alias only — extend the shared actions (served as <c>/api/pos/payment</c>)
    /// or add admin-only APIs under <c>/api/admin/*</c>. Sunset for legacy alias: 2026-09-30.
    /// </summary>
    [Obsolete(
        "Legacy HTTP alias /api/Payment/* is deprecated; clients must call /api/pos/payment/*. " +
        "This type still hosts the canonical /api/pos/payment routes (dual [Route]). " +
        "Do not add new endpoints that exist only on the legacy prefix. Sunset: 2026-09-30.",
        error: false)]
    [Route("api/[controller]")]
    [Route("api/pos/payment")]
    [ApiController]
    [ServiceFilter(typeof(LegacyRouteDeprecationFilter))]
    public class PaymentController : BaseController
    {
        private readonly IPaymentService _paymentService;
        private readonly IPaymentMethodCatalogService _paymentMethodCatalog;
        private readonly IPaymentHistoryService _paymentHistoryService;
        private readonly SignaturePipeline _signaturePipeline;
        private readonly IAuthorizationService _authorizationService;
        private readonly IRksvEnvironmentService _rksvEnvironment;

        private readonly IQrImageService _qrImageService;

        public PaymentController(
            IPaymentService paymentService,
            IPaymentMethodCatalogService paymentMethodCatalog,
            IPaymentHistoryService paymentHistoryService,
            IQrImageService qrImageService,
            SignaturePipeline signaturePipeline,
            IAuthorizationService authorizationService,
            IRksvEnvironmentService rksvEnvironment,
            ILogger<PaymentController> logger)
            : base(logger)
        {
            _paymentService = paymentService;
            _paymentMethodCatalog = paymentMethodCatalog;
            _paymentHistoryService = paymentHistoryService;
            _qrImageService = qrImageService;
            _signaturePipeline = signaturePipeline;
            _authorizationService = authorizationService;
            _rksvEnvironment = rksvEnvironment;
        }

        /// <summary>
        /// Mevcut ödeme yöntemlerini getir
        /// </summary>
        [HttpGet("methods")]
        [HasPermission(AppPermissions.PaymentView)]
        public async Task<IActionResult> GetPaymentMethods(
            [FromQuery] Guid cashRegisterId,
            CancellationToken cancellationToken)
        {
            try
            {
                // Kullanıcı ID'sini al
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return ErrorResponse("User not authenticated", 401);
                }

                if (cashRegisterId == Guid.Empty)
                {
                    return ErrorResponse("cashRegisterId is required", 400);
                }

                var paymentMethods = await _paymentMethodCatalog.GetActivePosMethodsAsync(cashRegisterId, cancellationToken);
                return SuccessResponse(paymentMethods, "Payment methods retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Get Payment Methods");
            }
        }

        /// <summary>
        /// Last 24 hours of payments for the POS cash register with backend-controlled storno/refund actions.
        /// </summary>
        [HttpGet("history")]
        [HasPermission(AppPermissions.PaymentView)]
        [ProducesResponseType(typeof(PaymentHistoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPaymentHistory(
            [FromQuery] Guid? cashRegisterId,
            [FromQuery] int hours = 24,
            [FromQuery] string language = "de",
            [FromQuery] int limit = 20,
            [FromQuery] int offset = 0,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return ErrorResponse("User not authenticated", 401);

                var actor = new PaymentHistoryActorContext(
                    userId,
                    User.GetActorRole(),
                    User.HasPermissionClaim(AppPermissions.PaymentCancel),
                    User.HasPermissionClaim(AppPermissions.RefundCreate));

                var (response, errorCode, errorMessage) = await _paymentHistoryService.GetRecentPaymentsAsync(
                    actor,
                    cashRegisterId,
                    hours,
                    language,
                    limit,
                    offset,
                    cancellationToken);

                if (response == null)
                    return ErrorResponse(errorMessage ?? "Payment history unavailable", 400, new { code = errorCode });

                return SuccessResponse(response, "Payment history retrieved successfully");
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Get Payment History");
            }
        }

        /// <summary>
        /// Yeni ödeme oluştur.
        /// Standard response envelope (success + errors + idempotency): send header <c>X-Regkasse-Payment-Contract: v2</c>.
        /// Without the header, legacy JSON shape is returned for backward compatibility.
        /// </summary>
        [HttpPost]
        [HasPermission(AppPermissions.PaymentTake)]
        [ProducesResponseType(typeof(PaymentApiEnvelope<PaymentCreateSuccessData>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(PaymentApiErrorBody), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(PaymentApiErrorBody), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(PaymentApiErrorBody), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(PaymentApiErrorBody), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            try
            {
                var v2 = PaymentApiContractMapper.WantsV2Contract(Request);
                var correlationId = PaymentApiContractMapper.GetCorrelationId(HttpContext);

                _logger.LogInformation(
                    "CreatePayment called: contractV2={V2}, items={ItemCount}, method={Method}, hasVoucherCode={HasVoucherCode}, voucherLineCount={VoucherLineCount}",
                    v2,
                    request.Items?.Count ?? 0,
                    request.Payment?.Method,
                    !string.IsNullOrWhiteSpace(request.Payment?.VoucherCode),
                    request.Payment?.VoucherRedemptions?.Count ?? 0);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    _logger.LogWarning("ModelState validation failed: {Errors}", string.Join("; ", errors));
                    if (v2)
                    {
                        return PaymentApiContractMapper.ValidationError(
                            "Model validation failed",
                            PaymentApiContractMapper.ModelStateToFieldErrors(ModelState),
                            correlationId);
                    }
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
                    if (v2)
                        return PaymentApiContractMapper.UnauthorizedError("User not authenticated", correlationId);
                    return ErrorResponse("User not authenticated", 401);
                }

                if (request.IsStorno)
                {
                    var stornoAuth = await _authorizationService.AuthorizeAsync(
                        User,
                        null,
                        PermissionCatalog.PolicyPrefix + AppPermissions.PaymentCancel);
                    if (!stornoAuth.Succeeded)
                        return Forbid();
                }

                if (request.IsRefund)
                {
                    var refundAuth = await _authorizationService.AuthorizeAsync(
                        User,
                        null,
                        PermissionCatalog.PolicyPrefix + AppPermissions.RefundCreate);
                    if (!refundAuth.Succeeded)
                        return Forbid();
                }

                var result = await _paymentService.CreatePaymentAsync(request, userId);

                if (result.Success)
                {
                    if (result.NonFiscalOfflineQueued)
                    {
                        if (v2)
                        {
                            var envelope = PaymentApiContractMapper.CreatePaymentSuccessEnvelope(
                                result,
                                sanitizedPayment: null,
                                correlationId,
                                request.IdempotencyKey);
                            return StatusCode(StatusCodes.Status202Accepted, envelope);
                        }

                        var offlineBody = new
                        {
                            success = true,
                            message = result.Message,
                            nonFiscalOfflineQueued = true,
                            offlineTransactionId = result.OfflineTransactionId,
                            invoicePersisted = false,
                            paymentId = (Guid?)null,
                            payment = (object?)null,
                            idempotentReplay = result.IdempotentReplay,
                            tse = (object?)null
                        };
                        return StatusCode(StatusCodes.Status202Accepted, offlineBody);
                    }

                    var paymentSafe = SanitizePaymentForResponse(result.Payment);
                    if (v2)
                    {
                        var envelope = PaymentApiContractMapper.CreatePaymentSuccessEnvelope(
                            result,
                            paymentSafe!,
                            correlationId,
                            request.IdempotencyKey);
                        return CreatedAtAction(nameof(GetPayment), new { id = result.Payment!.Id }, envelope);
                    }

                    var responseData = new
                    {
                        success = true,
                        paymentId = result.PaymentId,
                        message = result.Message,
                        payment = paymentSafe,
                        invoicePersisted = result.InvoicePersisted,
                        idempotentReplay = result.IdempotentReplay,
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

                if (v2)
                {
                    var err = PaymentApiContractMapper.CreatePaymentErrorBody(result, correlationId);
                    var status = PaymentApiContractMapper.MapToStatusCode(result);
                    return new ObjectResult(err) { StatusCode = status };
                }

                if (!string.IsNullOrEmpty(result.DiagnosticCode) && result.DiagnosticCode == CashRegisterResolutionCodes.Forbidden)
                    return StatusCode(403, new { success = false, message = result.Message, code = result.DiagnosticCode, details = new { errors = result.Errors } });
                if (!string.IsNullOrEmpty(result.DiagnosticCode) && result.DiagnosticCode == "BENEFIT_DAILY_ALLOWANCE_CONFLICT")
                    return ErrorResponse(result.Message, 409, new { code = "BENEFIT_DAILY_ALLOWANCE_CONFLICT", errors = result.Errors });
                if (!string.IsNullOrEmpty(result.DiagnosticCode))
                    return ErrorResponse(result.Message, 400, new { code = result.DiagnosticCode, errors = result.Errors, diagnosticCode = result.DiagnosticCode });
                return ErrorResponse(result.Message, 400, result.Errors);
            }
            catch (Exception ex)
            {
                return HandleException(ex, "Create Payment");
            }
        }

        /// <summary>
        /// Full receipt storno via dedicated route (sets <see cref="CreatePaymentRequest.IsStorno"/>). Same body contract as POST /; requires <c>payment.cancel</c> in addition to <c>payment.take</c>.
        /// </summary>
        [HttpPost("storno")]
        [HasPermission(AppPermissions.PaymentCancel)]
        public async Task<IActionResult> CreateStornoPayment([FromBody] CreateStornoPaymentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return await CreatePayment(request.ToCreatePaymentRequest());
        }

        /// <summary>
        /// Ödeme detaylarını getir
        /// </summary>
        [HttpGet("{id}")]
        [HasPermission(AppPermissions.PaymentView)]
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
        [HasPermission(AppPermissions.PaymentView)]
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
        [HasPermission(AppPermissions.PaymentView)]
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
        [HasPermission(AppPermissions.PaymentView)]
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
        [HasPermission(AppPermissions.PaymentCancel)]
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

                // TODO: scope check – ensure user can cancel this payment (e.g. same branch/cash register or manager).
                var result = await _paymentService.CancelPaymentAsync(
                    id,
                    request.Reason,
                    userId,
                    request.IdempotencyKey?.Trim(),
                    request.ReasonCode,
                    request.ApprovalToken?.Trim());

                if (result.Success)
                {
                    return SuccessResponse(result, "Payment cancelled successfully");
                }

                if (!string.IsNullOrEmpty(result.DiagnosticCode))
                    return ErrorResponse(result.Message, 400, MapReversalFailurePayload(result));
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
        [HasPermission(AppPermissions.RefundCreate)]
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

                var result = await _paymentService.RefundPaymentAsync(
                    id,
                    request.Amount,
                    request.Reason,
                    userId,
                    request.IdempotencyKey?.Trim(),
                    request.ReasonCode,
                    request.ApprovalToken?.Trim());

                if (result.Success)
                {
                    return SuccessResponse(result, "Refund processed successfully");
                }

                if (!string.IsNullOrEmpty(result.DiagnosticCode))
                    return ErrorResponse(result.Message, 400, MapReversalFailurePayload(result));
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
        [HasPermission(AppPermissions.PaymentView)]
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

                // Date-only and ISO inputs: PaymentService maps calendar Y/M/D to Austria inclusive day range (UTC half-open).
                var statistics = await _paymentService.GetPaymentStatisticsAsync(parsedStart, parsedEnd);

                return SuccessResponse(statistics, $"Retrieved payment statistics from {parsedStart:yyyy-MM-dd HH:mm} to {parsedEnd:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex)
            {
                return HandleException(ex, $"Get Payment Statistics from {startDate} to {endDate}");
            }
        }

        /// <summary>
        /// TSE QR kod görseli (PNG). Print/receipt için. Auth gerekli.
        /// </summary>
        /// <param name="id">Payment ID</param>
        /// <returns>256x256 PNG image</returns>
        /// <response code="200">PNG binary</response>
        /// <response code="404">Payment not found or no QR payload</response>
        [HttpGet("{id}/qr.png")]
        [HasPermission(AppPermissions.PaymentView)]
        [Produces("image/png")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetQrPng(Guid id)
        {
            var png = await _qrImageService.GetQrPngAsync(id);
            if (png == null || png.Length == 0)
                return ErrorResponse("Payment not found or QR payload unavailable", 404);

            Response.Headers.CacheControl = "private, max-age=300";
            return File(png, "image/png", $"qr-{id:N}.png");
        }

        /// <summary>
        /// TSE QR kod görseli (SVG). Vektörel, baskı için uygun. Auth gerekli.
        /// </summary>
        /// <param name="id">Payment ID</param>
        /// <returns>SVG vector graphic</returns>
        /// <response code="200">SVG content</response>
        /// <response code="404">Payment not found or no QR payload</response>
        [HttpGet("{id}/qr.svg")]
        [HasPermission(AppPermissions.PaymentView)]
        [Produces("image/svg+xml")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetQrSvg(Guid id)
        {
            var svg = await _qrImageService.GetQrSvgAsync(id);
            if (string.IsNullOrEmpty(svg))
                return ErrorResponse("Payment not found or QR payload unavailable", 404);

            Response.Headers.CacheControl = "private, max-age=300";
            var bytes = System.Text.Encoding.UTF8.GetBytes(svg);
            return File(bytes, "image/svg+xml", $"qr-{id:N}.svg");
        }

        /// <summary>
        /// Get receipt data for payment
        /// </summary>
        [HttpGet("{id}/receipt")]
        [HasPermission(AppPermissions.PaymentView)]
        public async Task<IActionResult> GetReceipt(Guid id)
        {
            try
            {
                var payment = await _paymentService.GetPaymentAsync(id);
                if (payment == null)
                {
                    return ErrorResponse($"Payment with ID {id} not found", 404);
                }

                var receiptData = await _paymentService.GetReceiptDataAsync(id, GetCurrentUserId());
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
        [HasPermission(AppPermissions.TseSign)]
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
        /// Manager oversight via <see cref="AppPermissions.PaymentView"/>.
        /// </summary>
        /// <param name="id">Payment ID</param>
        /// <returns>Checklist steps: CMC match, JWS format, Hash, Signature verify, Base64URL padding</returns>
        /// <response code="200">Diagnostic steps returned</response>
        /// <response code="404">Payment not found</response>
        [HttpGet("{id}/signature-debug")]
        [HasPermission(AppPermissions.PaymentView)]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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
                    var emptySteps = new[]
                    {
                        new SignatureDiagnosticStep(1, "CMC match", "FAIL", "No signature"),
                        new SignatureDiagnosticStep(2, "JWS format", "FAIL", "Empty TseSignature"),
                        new SignatureDiagnosticStep(3, "Hash", "FAIL", "N/A"),
                        new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "N/A"),
                        new SignatureDiagnosticStep(5, "Base64URL padding", "FAIL", "N/A")
                    };
                    return SuccessResponse(new { steps = emptySteps, compactJws = (string?)null }, "No signature on payment");
                }

                var steps = SignatureDiagnosticSimulation.ApplySimulationMode(
                    _signaturePipeline.VerifyDiagnostic(tseSignature),
                    _rksvEnvironment.IsTseSimulated(),
                    hasSignature: true);
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
            if (payment == null)
                return null;
            var json = JsonSerializer.Serialize(payment, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj)
                obj.Remove("tseSignature");
            return node;
        }

        private static object MapReversalFailurePayload(PaymentResult result) =>
            new
            {
                errors = result.Errors,
                diagnosticCode = result.DiagnosticCode,
                requiresApproval = result.RequiresApproval,
                approvalRequestId = result.ApprovalRequestId,
                approvalTokenExpiresAtUtc = result.ApprovalTokenExpiresAtUtc,
                approvalNotificationSent = result.ApprovalNotificationSent,
            };

        /// <summary>
        /// Manuel JWS doğrulama. RKSV Checklist 1–5 sonuçları.
        /// Manager oversight via <see cref="AppPermissions.PaymentView"/>.
        /// </summary>
        /// <param name="request">compactJws = COMPACT JWS string (header.payload.signature)</param>
        /// <returns>Structured diagnostic result</returns>
        /// <response code="200">Verify result with checklist steps</response>
        [HttpPost("verify-signature")]
        [HasPermission(AppPermissions.PaymentView)]
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
