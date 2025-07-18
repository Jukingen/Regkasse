using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Services;
using System;
using System.Threading.Tasks;
using Registrierkasse_API.Data;
using Microsoft.EntityFrameworkCore;

namespace Registrierkasse_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TseController : ControllerBase
    {
        private readonly ITseService _tseService;
        private readonly ILogger<TseController> _logger;
        private readonly AppDbContext _context;

        public TseController(ITseService tseService, ILogger<TseController> logger, AppDbContext context)
        {
            _tseService = tseService;
            _logger = logger;
            _context = context;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                // Demo TSE durumu
                var tseStatus = new
                {
                    id = "tse-demo-001",
                    deviceName = "Demo TSE Device",
                    serialNumber = "TSE-DEMO-123456",
                    firmwareVersion = "1.2.3",
                    isConnected = true,
                    lastSignatureCounter = 12345,
                    lastSignatureTime = DateTime.UtcNow.AddMinutes(-5),
                    memoryStatus = "Normal",
                    certificateStatus = "Valid",
                    certificateExpiry = DateTime.UtcNow.AddYears(1),
                    dailyReportStatus = "Completed",
                    lastDailyReport = DateTime.UtcNow.AddHours(-2)
                };

                return Ok(tseStatus);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve TSE status", details = ex.Message });
            }
        }

        [HttpPost("daily-report")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<TseSignatureResult>> GenerateDailyReport()
        {
            try
            {
                var result = await _tseService.SignDailyReportAsync();
                _logger.LogInformation("Günlük rapor imzalandı: {Signature}", result.Signature);
                return Ok(result);
            }
            catch (TseException ex)
            {
                _logger.LogError(ex, "Günlük rapor imzalama hatası");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Günlük rapor imzalama beklenmeyen hata");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("validate")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<IActionResult> ValidateSignature([FromBody] ValidateSignatureModel model)
        {
            try
            {
                var isValid = await _tseService.ValidateSignatureAsync(model.Signature, model.ProcessData);
                return Ok(new { isValid });
            }
            catch (TseException ex)
            {
                _logger.LogError(ex, "TSE imza doğrulama başarısız");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("nullbeleg")]
        [Authorize(Roles = "Administrator,Manager")]
        public async Task<ActionResult<TseSignatureResult>> SignNullbeleg([FromBody] SignNullbelegModel model)
        {
            try
            {
                var result = await _tseService.SignNullbelegAsync(model.Date, model.CashRegisterId);
                _logger.LogInformation("Sıfır beleg imzalandı: {Signature}", result.Signature);
                return Ok(result);
            }
            catch (TseException ex)
            {
                _logger.LogError(ex, "Sıfır beleg imzalama hatası");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sıfır beleg imzalama beklenmeyen hata");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    public class ValidateSignatureModel
    {
        public string Signature { get; set; } = string.Empty;
        public string ProcessData { get; set; } = string.Empty;
    }

    public class SignNullbelegModel
    {
        public DateTime Date { get; set; }
        public string CashRegisterId { get; set; } = string.Empty;
    }
} 
