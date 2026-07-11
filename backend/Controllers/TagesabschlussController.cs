using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagesabschlussController : ControllerBase
    {
        private readonly ITagesabschlussService _tagesabschlussService;
        private readonly IDailyClosingReportService _dailyClosingReportService;
        private readonly ISettingsTenantResolver _settingsTenantResolver;
        private readonly ILogger<TagesabschlussController> _logger;

        public TagesabschlussController(
            ITagesabschlussService tagesabschlussService,
            IDailyClosingReportService dailyClosingReportService,
            ISettingsTenantResolver settingsTenantResolver,
            ILogger<TagesabschlussController> logger)
        {
            _tagesabschlussService = tagesabschlussService;
            _dailyClosingReportService = dailyClosingReportService;
            _settingsTenantResolver = settingsTenantResolver;
            _logger = logger;
        }

        /// <summary>
        /// Perform daily closing for the current day
        /// </summary>
        [HttpPost("daily")]
        [HasPermission(AppPermissions.DailyClosingExecute)]
        [ProducesResponseType(typeof(TagesabschlussResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TagesabschlussResult>> PerformDailyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.GetActorUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new TagesabschlussErrorResponse { error = "User ID not found in token" });
                }

                var result = await _tagesabschlussService.PerformDailyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Daily closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new TagesabschlussErrorResponse
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Perform monthly closing for the current month
        /// </summary>
        [HttpPost("monthly")]
        [HasPermission(AppPermissions.DailyClosingExecute)]
        [ProducesResponseType(typeof(TagesabschlussResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TagesabschlussResult>> PerformMonthlyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.GetActorUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new TagesabschlussErrorResponse { error = "User ID not found in token" });
                }

                var result = await _tagesabschlussService.PerformMonthlyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Monthly closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new TagesabschlussErrorResponse
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Perform yearly closing for the current year
        /// </summary>
        [HttpPost("yearly")]
        [HasPermission(AppPermissions.DailyClosingExecute)]
        [ProducesResponseType(typeof(TagesabschlussResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TagesabschlussResult>> PerformYearlyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.GetActorUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new TagesabschlussErrorResponse { error = "User ID not found in token" });
                }

                var result = await _tagesabschlussService.PerformYearlyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Yearly closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new TagesabschlussErrorResponse
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>Localized PDF report for a completed RKSV closing (Daily/Monthly/Yearly).</summary>
        [HttpGet("closing/{closingId:guid}/report.pdf")]
        [HasPermission(AppPermissions.DailyClosingView)]
        [Produces("application/pdf")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClosingReportPdf(
            Guid closingId,
            [FromQuery] string? language,
            CancellationToken cancellationToken)
        {
            var pdf = await _dailyClosingReportService.TryGenerateClosingReportPdfAsync(
                closingId,
                actorUserId: null,
                language ?? "de",
                cancellationToken);

            if (pdf == null || pdf.Length == 0)
                return NotFound(new { error = "Closing report not found" });

            var fileName = $"RKSV-Abschluss_{closingId:N}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        /// <summary>
        /// Get closing history for the authenticated user
        /// </summary>
        [HttpGet("history")]
        [HasPermission(AppPermissions.DailyClosingView)]
        [ProducesResponseType(typeof(List<TagesabschlussResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<TagesabschlussResult>>> GetClosingHistory(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? cashRegisterId,
            CancellationToken cancellationToken)
        {
            try
            {
                var registerResolution = await ResolveOperationalCashRegisterAsync(cashRegisterId, cancellationToken);
                if (registerResolution.ErrorResult is { } error)
                    return error;

                var history = await _tagesabschlussService.GetClosingHistoryAsync(
                    fromDate,
                    toDate,
                    registerResolution.RegisterId!.Value,
                    cancellationToken);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Check if daily closing can be performed for a cash register
        /// </summary>
        [HttpGet("can-close/{cashRegisterId}")]
        [HasPermission(AppPermissions.DailyClosingView)]
        [ProducesResponseType(typeof(TagesabschlussCanCloseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TagesabschlussCanCloseResponse>> CanPerformClosing(Guid cashRegisterId)
        {
            try
            {
                var canClose = await _tagesabschlussService.CanPerformClosingAsync(cashRegisterId);
                var canCloseMonthly = await _tagesabschlussService.CanPerformMonthlyClosingAsync(cashRegisterId);
                var canCloseYearly = await _tagesabschlussService.CanPerformYearlyClosingAsync(cashRegisterId);
                var lastClosingDate = await _tagesabschlussService.GetLastClosingDateAsync(cashRegisterId);
                var lastMonthlyClosingDate =
                    await _tagesabschlussService.GetLastClosingDateForTypeAsync(cashRegisterId, "Monthly");
                var lastYearlyClosingDate =
                    await _tagesabschlussService.GetLastClosingDateForTypeAsync(cashRegisterId, "Yearly");
                var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
                var (dayStartUtc, dayEndExclusiveUtc) =
                    PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
                var paymentsWithoutInvoiceCount =
                    await _tagesabschlussService.GetPaymentsWithoutInvoiceCountAsync(
                        cashRegisterId, dayStartUtc, dayEndExclusiveUtc);

                string message = canClose
                    ? "Daily closing can be performed"
                    : (paymentsWithoutInvoiceCount > 0
                        ? $"{paymentsWithoutInvoiceCount} payment(s) without invoice; resolve before closing."
                        : "Daily closing already performed for today");

                return Ok(new TagesabschlussCanCloseResponse
                {
                    canClose = canClose,
                    canCloseMonthly = canCloseMonthly,
                    canCloseYearly = canCloseYearly,
                    lastClosingDate = lastClosingDate,
                    lastMonthlyClosingDate = lastMonthlyClosingDate,
                    lastYearlyClosingDate = lastYearlyClosingDate,
                    paymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Get closing statistics for a specific period
        /// </summary>
        [HttpGet("statistics")]
        [HasPermission(AppPermissions.DailyClosingView)]
        [ProducesResponseType(typeof(TagesabschlussStatisticsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(TagesabschlussErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TagesabschlussStatisticsResponse>> GetClosingStatistics(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? cashRegisterId,
            CancellationToken cancellationToken)
        {
            try
            {
                var registerResolution = await ResolveOperationalCashRegisterAsync(cashRegisterId, cancellationToken);
                if (registerResolution.ErrorResult is { } error)
                    return error;

                var history = await _tagesabschlussService.GetClosingHistoryAsync(
                    fromDate,
                    toDate,
                    registerResolution.RegisterId!.Value,
                    cancellationToken);

                var statistics = new TagesabschlussStatisticsResponse
                {
                    totalClosings = history.Count,
                    totalAmount = history.Sum(h => h.TotalAmount),
                    totalTaxAmount = history.Sum(h => h.TotalTaxAmount),
                    totalTransactions = history.Sum(h => h.TransactionCount),
                    averageDailyAmount = history.Where(h => h.ClosingType == "Daily").Any()
                        ? history.Where(h => h.ClosingType == "Daily").Average(h => h.TotalAmount)
                        : 0,
                    lastClosingDate = history.OrderByDescending(h => h.ClosingDate).FirstOrDefault()?.ClosingDate
                };

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new TagesabschlussErrorResponse { error = "Internal server error", details = ex.Message });
            }
        }

        private async Task<(Guid? RegisterId, ActionResult? ErrorResult)> ResolveOperationalCashRegisterAsync(
            Guid? cashRegisterId,
            CancellationToken cancellationToken)
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            if (tenantId == Guid.Empty)
            {
                return (null, BadRequest(new TagesabschlussErrorResponse
                {
                    error = "Tenant context required",
                    details = "TENANT_CONTEXT_REQUIRED",
                }));
            }

            var resolved = await _tagesabschlussService.ResolveOperationalCashRegisterIdAsync(
                tenantId,
                cashRegisterId,
                cancellationToken);
            if (!resolved.HasValue)
            {
                return (null, NotFound(new TagesabschlussErrorResponse
                {
                    error = "No cash register found for this tenant",
                    details = "TAGESABSCHLUSS_NO_REGISTER",
                }));
            }

            return (resolved.Value, null);
        }
    }

    public class DailyClosingRequest
    {
        [Required]
        public Guid CashRegisterId { get; set; }
    }

    public class TagesabschlussCanCloseResponse
    {
        [Required]
        public bool canClose { get; set; }
        [Required]
        public bool canCloseMonthly { get; set; }
        [Required]
        public bool canCloseYearly { get; set; }
        public DateTime? lastClosingDate { get; set; }
        public DateTime? lastMonthlyClosingDate { get; set; }
        public DateTime? lastYearlyClosingDate { get; set; }
        [Required]
        public int paymentsWithoutInvoiceCount { get; set; }
        public string? message { get; set; }
    }

    public class TagesabschlussStatisticsResponse
    {
        [Required]
        public int totalClosings { get; set; }
        [Required]
        public decimal totalAmount { get; set; }
        [Required]
        public decimal totalTaxAmount { get; set; }
        [Required]
        public int totalTransactions { get; set; }
        [Required]
        public decimal averageDailyAmount { get; set; }
        public DateTime? lastClosingDate { get; set; }
    }

    public class TagesabschlussErrorResponse
    {
        public string? error { get; set; }
        public string? details { get; set; }
        public int? paymentsWithoutInvoiceCount { get; set; }
    }
}
