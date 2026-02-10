using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FinanzOnlineController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FinanzOnlineController> _logger;

        public FinanzOnlineController(AppDbContext context, ILogger<FinanzOnlineController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/finanzonline/config
        [HttpGet("config")]
        public async Task<ActionResult<FinanzOnlineConfigResponse>> GetConfig()
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (companySettings == null)
                {
                    return NotFound(new { message = "Firma ayarları bulunamadı" });
                }

                return Ok(new FinanzOnlineConfigResponse
                {
                    ApiUrl = companySettings.FinanzOnlineApiUrl ?? "",
                    Username = companySettings.FinanzOnlineUsername ?? "",
                    AutoSubmit = companySettings.FinanzOnlineAutoSubmit,
                    SubmitInterval = companySettings.FinanzOnlineSubmitInterval,
                    RetryAttempts = companySettings.FinanzOnlineRetryAttempts,
                    EnableValidation = companySettings.FinanzOnlineEnableValidation,
                    IsEnabled = tseDevice?.FinanzOnlineEnabled ?? false
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline config fetch failed");
                return StatusCode(500, new { message = "FinanzOnline konfigürasyonu alınamadı" });
            }
        }

        // PUT: api/finanzonline/config
        [HttpPut("config")]
        public async Task<ActionResult<FinanzOnlineConfigResponse>> UpdateConfig([FromBody] FinanzOnlineConfigRequest request)
        {
            try
            {
                var companySettings = await _context.CompanySettings.FirstOrDefaultAsync();
                if (companySettings == null)
                {
                    return NotFound(new { message = "Firma ayarları bulunamadı" });
                }

                // Konfigürasyon güncelleme
                companySettings.FinanzOnlineApiUrl = request.ApiUrl;
                companySettings.FinanzOnlineUsername = request.Username;
                companySettings.FinanzOnlineAutoSubmit = request.AutoSubmit;
                companySettings.FinanzOnlineSubmitInterval = request.SubmitInterval;
                companySettings.FinanzOnlineRetryAttempts = request.RetryAttempts;
                companySettings.FinanzOnlineEnableValidation = request.EnableValidation;
                companySettings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("FinanzOnline config updated by user");

                return Ok(new FinanzOnlineConfigResponse
                {
                    ApiUrl = companySettings.FinanzOnlineApiUrl ?? "",
                    Username = companySettings.FinanzOnlineUsername ?? "",
                    AutoSubmit = companySettings.FinanzOnlineAutoSubmit,
                    SubmitInterval = companySettings.FinanzOnlineSubmitInterval,
                    RetryAttempts = companySettings.FinanzOnlineRetryAttempts,
                    EnableValidation = companySettings.FinanzOnlineEnableValidation,
                    IsEnabled = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline config update failed");
                return StatusCode(500, new { message = "FinanzOnline konfigürasyonu güncellenemedi" });
            }
        }

        // GET: api/finanzonline/status
        [HttpGet("status")]
        public async Task<ActionResult<FinanzOnlineStatusResponse>> GetStatus()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return Ok(new FinanzOnlineStatusResponse
                    {
                        IsConnected = false,
                        ApiVersion = "",
                        LastSync = "",
                        PendingInvoices = 0,
                        PendingReports = 0,
                        ErrorMessage = "FinanzOnline etkin değil"
                    });
                }

                // FinanzOnline bağlantı durumu simülasyonu
                bool isConnected = await CheckFinanzOnlineConnection(tseDevice);

                return Ok(new FinanzOnlineStatusResponse
                {
                    IsConnected = isConnected,
                    ApiVersion = "1.0",
                    LastSync = tseDevice.LastFinanzOnlineSync.ToString("yyyy-MM-ddTHH:mm:ss"),
                    PendingInvoices = tseDevice.PendingInvoices,
                    PendingReports = tseDevice.PendingReports,
                    ErrorMessage = isConnected ? null : "FinanzOnline API'ye bağlanılamadı"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline status check failed");
                return StatusCode(500, new { message = "FinanzOnline durumu kontrol edilemedi" });
            }
        }

        // POST: api/finanzonline/submit-invoice
        [HttpPost("submit-invoice")]
        public async Task<ActionResult<FinanzOnlineSubmitResponse>> SubmitInvoice([FromBody] FinanzOnlineSubmitRequest request)
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled && t.IsConnected)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return BadRequest(new { message = "FinanzOnline etkin değil veya TSE cihazı bağlı değil" });
                }

                // Fatura gönderimi simülasyonu
                bool submitSuccess = await SubmitInvoiceToFinanzOnline(tseDevice, request);
                
                if (submitSuccess)
                {
                    tseDevice.LastFinanzOnlineSync = DateTime.UtcNow;
                    tseDevice.PendingInvoices = Math.Max(0, tseDevice.PendingInvoices - 1);
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Invoice submitted to FinanzOnline: {InvoiceNumber}", request.InvoiceNumber);
                    
                    return Ok(new FinanzOnlineSubmitResponse
                    {
                        Success = true,
                        Message = "Fatura FinanzOnline'a başarıyla gönderildi",
                        SubmissionId = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
                    });
                }
                else
                {
                    return BadRequest(new { message = "Fatura FinanzOnline'a gönderilemedi" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline invoice submission failed");
                return StatusCode(500, new { message = "Fatura gönderimi başarısız" });
            }
        }

        // GET: api/finanzonline/errors
        [HttpGet("errors")]
        public async Task<ActionResult<List<FinanzOnlineErrorResponse>>> GetErrors()
        {
            try
            {
                // Son hataları getir (gerçek implementasyonda ayrı tablo kullanılır)
                var errors = new List<FinanzOnlineErrorResponse>
                {
                    new FinanzOnlineErrorResponse
                    {
                        Code = "API_001",
                        Message = "API bağlantısı başarısız",
                        Timestamp = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss"),
                        InvoiceNumber = "",
                        RetryCount = 2
                    }
                };

                return Ok(errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline errors fetch failed");
                return StatusCode(500, new { message = "FinanzOnline hataları alınamadı" });
            }
        }

        // POST: api/finanzonline/test-connection
        [HttpPost("test-connection")]
        public async Task<ActionResult<FinanzOnlineTestResponse>> TestConnection()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.FinanzOnlineEnabled)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return BadRequest(new { message = "FinanzOnline etkin değil" });
                }

                // Bağlantı testi simülasyonu
                bool isConnected = await CheckFinanzOnlineConnection(tseDevice);

                return Ok(new FinanzOnlineTestResponse
                {
                    Success = isConnected,
                    Message = isConnected ? "FinanzOnline bağlantısı başarılı" : "FinanzOnline bağlantısı başarısız",
                    ApiVersion = "1.0",
                    ResponseTime = 150, // ms
                    Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinanzOnline connection test failed");
                return StatusCode(500, new { message = "FinanzOnline bağlantı testi başarısız" });
            }
        }

        // Private helper methods
        private async Task<bool> CheckFinanzOnlineConnection(TseDevice device)
        {
            // Gerçek implementasyonda FinanzOnline API'ye bağlantı testi yapılır
            await Task.Delay(100); // Simülasyon için kısa bekleme
            
            // Basit bağlantı kontrolü simülasyonu
            return !string.IsNullOrEmpty(device.FinanzOnlineUsername);
        }

        private async Task<bool> SubmitInvoiceToFinanzOnline(TseDevice device, FinanzOnlineSubmitRequest request)
        {
            // Gerçek implementasyonda FinanzOnline API'ye fatura gönderilir
            await Task.Delay(300); // Simülasyon için kısa bekleme
            
            // Fatura gönderimi simülasyonu
            return !string.IsNullOrEmpty(request.InvoiceNumber) && request.TotalAmount > 0;
        }
    }

    // Request/Response Models
    public class FinanzOnlineConfigRequest
    {
        [Required]
        public string ApiUrl { get; set; } = string.Empty;
        
        [Required]
        public string Username { get; set; } = string.Empty;
        
        public bool AutoSubmit { get; set; } = false;
        public int SubmitInterval { get; set; } = 60; // dakika
        public int RetryAttempts { get; set; } = 3;
        public bool EnableValidation { get; set; } = true;
    }

    public class FinanzOnlineConfigResponse
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool AutoSubmit { get; set; }
        public int SubmitInterval { get; set; }
        public int RetryAttempts { get; set; }
        public bool EnableValidation { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class FinanzOnlineStatusResponse
    {
        public bool IsConnected { get; set; }
        public string ApiVersion { get; set; } = string.Empty;
        public string LastSync { get; set; } = string.Empty;
        public int PendingInvoices { get; set; }
        public int PendingReports { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class FinanzOnlineSubmitRequest
    {
        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public string TseSignature { get; set; } = string.Empty;
        
        [Required]
        public string TaxDetails { get; set; } = string.Empty;
        
        [Required]
        public DateTime InvoiceDate { get; set; }
        
        [Required]
        public string KassenId { get; set; } = string.Empty;
    }

    public class FinanzOnlineSubmitResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    public class FinanzOnlineErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public int RetryCount { get; set; }
    }

    public class FinanzOnlineTestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ApiVersion { get; set; } = string.Empty;
        public int ResponseTime { get; set; }
        public string Timestamp { get; set; } = string.Empty;
    }
}
