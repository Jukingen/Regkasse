using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse.Services;
using System;
using System.Threading.Tasks;

namespace Registrierkasse.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [Route("api/[controller]")]
    [ApiController]
    public class TseController : ControllerBase
    {
        private readonly ITseService _tseService;
        private readonly ILogger<TseController> _logger;

        public TseController(ITseService tseService, ILogger<TseController> logger)
        {
            _tseService = tseService;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _tseService.GetStatusAsync();
                return Ok(status);
            }
            catch (TseException ex)
            {
                _logger.LogError(ex, "TSE durum bilgisi alınamadı");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("daily-report")]
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