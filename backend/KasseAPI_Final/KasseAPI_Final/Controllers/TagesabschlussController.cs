using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Services;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TagesabschlussController : ControllerBase
    {
        private readonly ITagesabschlussService _tagesabschlussService;

        public TagesabschlussController(ITagesabschlussService tagesabschlussService)
        {
            _tagesabschlussService = tagesabschlussService;
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
                    return BadRequest(new { error = result.ErrorMessage });
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
                    return BadRequest(new { error = result.ErrorMessage });
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
                    return BadRequest(new { error = result.ErrorMessage });
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
        public async Task<IActionResult> GetClosingHistory([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var history = await _tagesabschlussService.GetClosingHistoryAsync(userId, fromDate, toDate);
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

                return Ok(new
                {
                    canClose,
                    lastClosingDate,
                    message = canClose ? "Daily closing can be performed" : "Daily closing already performed for today"
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
        public async Task<IActionResult> GetClosingStatistics([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var history = await _tagesabschlussService.GetClosingHistoryAsync(userId, fromDate, toDate);
                
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
