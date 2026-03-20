using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [HasPermission(AppPermissions.TseSign)]
    public class TagesabschlussController : ControllerBase
    {
        private readonly ITagesabschlussService _tagesabschlussService;
        private readonly ILogger<TagesabschlussController> _logger;

        public TagesabschlussController(ITagesabschlussService tagesabschlussService, ILogger<TagesabschlussController> logger)
        {
            _tagesabschlussService = tagesabschlussService;
            _logger = logger;
        }

        /// <summary>
        /// Perform daily closing for the current day
        /// </summary>
        [HttpPost("daily")]
        public async Task<IActionResult> PerformDailyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var result = await _tagesabschlussService.PerformDailyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Daily closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Perform monthly closing for the current month
        /// </summary>
        [HttpPost("monthly")]
        public async Task<IActionResult> PerformMonthlyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var result = await _tagesabschlussService.PerformMonthlyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Monthly closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Perform yearly closing for the current year
        /// </summary>
        [HttpPost("yearly")]
        public async Task<IActionResult> PerformYearlyClosing([FromBody] DailyClosingRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var result = await _tagesabschlussService.PerformYearlyClosingAsync(userId, request.CashRegisterId);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Yearly closing blocked: {Reason}", result.ErrorMessage);
                    return BadRequest(new
                    {
                        error = result.ErrorMessage,
                        paymentsWithoutInvoiceCount = result.PaymentsWithoutInvoiceCount
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Get closing history for the authenticated user
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetClosingHistory([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] Guid? cashRegisterId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var history = await _tagesabschlussService.GetClosingHistoryAsync(userId, fromDate, toDate, cashRegisterId);
                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Check if daily closing can be performed for a cash register
        /// </summary>
        [HttpGet("can-close/{cashRegisterId}")]
        public async Task<IActionResult> CanPerformClosing(Guid cashRegisterId)
        {
            try
            {
                var canClose = await _tagesabschlussService.CanPerformClosingAsync(cashRegisterId);
                var lastClosingDate = await _tagesabschlussService.GetLastClosingDateAsync(cashRegisterId);
                var today = DateTime.Today;
                var paymentsWithoutInvoiceCount = await _tagesabschlussService.GetPaymentsWithoutInvoiceCountAsync(cashRegisterId, today, today.AddDays(1));

                string message = canClose
                    ? "Daily closing can be performed"
                    : (paymentsWithoutInvoiceCount > 0
                        ? $"{paymentsWithoutInvoiceCount} payment(s) without invoice; resolve before closing."
                        : "Daily closing already performed for today");

                return Ok(new
                {
                    canClose,
                    lastClosingDate,
                    paymentsWithoutInvoiceCount,
                    message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        /// <summary>
        /// Get closing statistics for a specific period
        /// </summary>
        [HttpGet("statistics")]
        public async Task<IActionResult> GetClosingStatistics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] Guid? cashRegisterId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var history = await _tagesabschlussService.GetClosingHistoryAsync(userId, fromDate, toDate, cashRegisterId);
                
                var statistics = new
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
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }
    }

    public class DailyClosingRequest
    {
        [Required]
        public Guid CashRegisterId { get; set; }
    }
}
