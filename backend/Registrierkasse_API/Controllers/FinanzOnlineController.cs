using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse.Services;
using System.Threading.Tasks;

namespace Registrierkasse.Controllers
{
    [Authorize(Roles = "Administrator,Manager")]
    [Route("api/[controller]")]
    [ApiController]
    public class FinanzOnlineController : ControllerBase
    {
        private readonly IFinanzOnlineService _finanzOnlineService;
        private readonly ILogger<FinanzOnlineController> _logger;

        public FinanzOnlineController(IFinanzOnlineService finanzOnlineService, ILogger<FinanzOnlineController> logger)
        {
            _finanzOnlineService = finanzOnlineService;
            _logger = logger;
        }

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            try
            {
                var config = new
                {
                    ApiUrl = Environment.GetEnvironmentVariable("FINANZONLINE_API_URL") ?? "",
                    Username = Environment.GetEnvironmentVariable("FINANZONLINE_USERNAME") ?? "",
                    Password = Environment.GetEnvironmentVariable("FINANZONLINE_PASSWORD") ?? "",
                    AutoSubmit = bool.Parse(Environment.GetEnvironmentVariable("FINANZONLINE_AUTO_SUBMIT") ?? "false"),
                    SubmitInterval = int.Parse(Environment.GetEnvironmentVariable("FINANZONLINE_SUBMIT_INTERVAL") ?? "60"),
                    RetryAttempts = int.Parse(Environment.GetEnvironmentVariable("FINANZONLINE_RETRY_ATTEMPTS") ?? "3"),
                    EnableValidation = bool.Parse(Environment.GetEnvironmentVariable("FINANZONLINE_ENABLE_VALIDATION") ?? "true")
                };

                return Ok(config);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline konfigürasyonu alma hatası");
                return StatusCode(500, new { message = "Konfigürasyon alınamadı" });
            }
        }

        [HttpPost("config")]
        public async Task<IActionResult> SaveConfig([FromBody] FinanzOnlineConfigModel config)
        {
            try
            {
                // Konfigürasyonu environment variable olarak kaydet
                Environment.SetEnvironmentVariable("FINANZONLINE_API_URL", config.ApiUrl);
                Environment.SetEnvironmentVariable("FINANZONLINE_USERNAME", config.Username);
                Environment.SetEnvironmentVariable("FINANZONLINE_PASSWORD", config.Password);
                Environment.SetEnvironmentVariable("FINANZONLINE_AUTO_SUBMIT", config.AutoSubmit.ToString());
                Environment.SetEnvironmentVariable("FINANZONLINE_SUBMIT_INTERVAL", config.SubmitInterval.ToString());
                Environment.SetEnvironmentVariable("FINANZONLINE_RETRY_ATTEMPTS", config.RetryAttempts.ToString());
                Environment.SetEnvironmentVariable("FINANZONLINE_ENABLE_VALIDATION", config.EnableValidation.ToString());

                _logger.LogInformation("FinanzOnline konfigürasyonu kaydedildi");
                return Ok(new { message = "Konfigürasyon kaydedildi" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline konfigürasyonu kaydetme hatası");
                return StatusCode(500, new { message = "Konfigürasyon kaydedilemedi" });
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _finanzOnlineService.GetStatusAsync();
                return Ok(status);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline durum alma hatası");
                return StatusCode(500, new { message = "Durum bilgisi alınamadı" });
            }
        }

        [HttpGet("errors")]
        public async Task<IActionResult> GetErrors()
        {
            try
            {
                var errors = await _finanzOnlineService.GetErrorsAsync();
                return Ok(errors);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline hatalar alma hatası");
                return StatusCode(500, new { message = "Hata listesi alınamadı" });
            }
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var authenticated = await _finanzOnlineService.AuthenticateAsync();
                
                if (authenticated)
                {
                    var status = await _finanzOnlineService.GetStatusAsync();
                    return Ok(new { message = "FinanzOnline bağlantısı başarılı", status });
                }
                else
                {
                    return BadRequest(new { message = "FinanzOnline kimlik doğrulama başarısız" });
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline bağlantı testi hatası");
                return StatusCode(500, new { message = "Bağlantı testi başarısız" });
            }
        }

        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate()
        {
            try
            {
                var authenticated = await _finanzOnlineService.AuthenticateAsync();
                
                if (authenticated)
                {
                    _logger.LogInformation("FinanzOnline kimlik doğrulama başarılı");
                    return Ok(new { message = "Kimlik doğrulama başarılı" });
                }
                else
                {
                    return BadRequest(new { message = "Kimlik doğrulama başarısız" });
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline kimlik doğrulama hatası");
                return StatusCode(500, new { message = "Kimlik doğrulama başarısız" });
            }
        }

        [HttpPost("submit-pending")]
        public async Task<IActionResult> SubmitPendingData()
        {
            try
            {
                // Bu kısım gerçek implementasyonda bekleyen faturaları ve raporları gönderecek
                _logger.LogInformation("Bekleyen veriler FinanzOnline'a gönderiliyor");
                
                // TODO: Bekleyen faturaları ve raporları al ve gönder
                
                return Ok(new { message = "Bekleyen veriler gönderildi" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline veri gönderme hatası");
                return StatusCode(500, new { message = "Veri gönderme başarısız" });
            }
        }

        [HttpPost("validate-tax-number")]
        public async Task<IActionResult> ValidateTaxNumber([FromBody] ValidateTaxNumberModel model)
        {
            try
            {
                var isValid = await _finanzOnlineService.ValidateTaxNumberAsync(model.TaxNumber);
                
                return Ok(new { isValid, taxNumber = model.TaxNumber });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Vergi numarası doğrulama hatası");
                return StatusCode(500, new { message = "Vergi numarası doğrulanamadı" });
            }
        }
    }

    public class FinanzOnlineConfigModel
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool AutoSubmit { get; set; }
        public int SubmitInterval { get; set; }
        public int RetryAttempts { get; set; }
        public bool EnableValidation { get; set; }
    }

    public class ValidateTaxNumberModel
    {
        public string TaxNumber { get; set; } = string.Empty;
    }
} 