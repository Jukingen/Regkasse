using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TseController> _logger;

        public TseController(AppDbContext context, ILogger<TseController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/tse/status
        [HttpGet("status")]
        public async Task<ActionResult<TseStatusResponse>> GetTseStatus()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive)
                    .OrderByDescending(t => t.LastConnectionTime)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return Ok(new TseStatusResponse
                    {
                        IsConnected = false,
                        SerialNumber = "",
                        CertificateStatus = "UNKNOWN",
                        MemoryStatus = "UNKNOWN",
                        LastSignatureTime = "",
                        CanCreateInvoices = false,
                        ErrorMessage = "TSE cihazı bulunamadı",
                        KassenId = "",
                        FinanzOnlineEnabled = false
                    });
                }

                return Ok(new TseStatusResponse
                {
                    IsConnected = tseDevice.IsConnected,
                    SerialNumber = tseDevice.SerialNumber,
                    CertificateStatus = tseDevice.CertificateStatus,
                    MemoryStatus = tseDevice.MemoryStatus,
                    LastSignatureTime = tseDevice.LastSignatureTime.ToString("HH:mm:ss"),
                    CanCreateInvoices = tseDevice.CanCreateInvoices,
                    ErrorMessage = tseDevice.ErrorMessage,
                    KassenId = tseDevice.KassenId.ToString(),
                    FinanzOnlineEnabled = tseDevice.FinanzOnlineEnabled
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE status check failed");
                return StatusCode(500, new { message = "TSE durumu kontrol edilemedi" });
            }
        }

        // POST: api/tse/connect
        [HttpPost("connect")]
        public async Task<ActionResult<TseConnectionResponse>> ConnectTseDevice([FromBody] TseConnectionRequest request)
        {
            try
            {
                // TSE cihazı bağlantı simülasyonu (gerçek implementasyonda USB bağlantısı kontrol edilir)
                var tseDevice = await _context.TseDevices
                    .Where(t => t.SerialNumber == request.SerialNumber && t.IsActive)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return NotFound(new { message = "TSE cihazı bulunamadı" });
                }

                // Bağlantı kontrolü simülasyonu
                bool isConnected = await SimulateTseConnection(tseDevice);
                
                if (isConnected)
                {
                    tseDevice.IsConnected = true;
                    tseDevice.LastConnectionTime = DateTime.UtcNow;
                    tseDevice.CanCreateInvoices = true;
                    tseDevice.ErrorMessage = null;
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("TSE device connected: {SerialNumber}", tseDevice.SerialNumber);
                    
                    return Ok(new TseConnectionResponse
                    {
                        Success = true,
                        Message = "TSE cihazı başarıyla bağlandı",
                        DeviceInfo = new TseDeviceInfo
                        {
                            SerialNumber = tseDevice.SerialNumber,
                            DeviceType = tseDevice.DeviceType,
                            KassenId = tseDevice.KassenId.ToString(),
                            CertificateStatus = tseDevice.CertificateStatus,
                            MemoryStatus = tseDevice.MemoryStatus
                        }
                    });
                }
                else
                {
                    tseDevice.IsConnected = false;
                    tseDevice.CanCreateInvoices = false;
                    tseDevice.ErrorMessage = "TSE cihazına bağlanılamadı";
                    
                    await _context.SaveChangesAsync();
                    
                    return BadRequest(new TseConnectionResponse
                    {
                        Success = false,
                        Message = "TSE cihazına bağlanılamadı",
                        ErrorMessage = "USB bağlantısı veya cihaz yanıt vermiyor"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE connection failed");
                return StatusCode(500, new { message = "TSE bağlantısı başarısız" });
            }
        }

        // POST: api/tse/signature
        [HttpPost("signature")]
        public async Task<ActionResult<TseSignatureResponse>> CreateTseSignature([FromBody] TseSignatureRequest request)
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.IsConnected && t.CanCreateInvoices)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return BadRequest(new { message = "TSE cihazı bağlı değil veya fatura oluşturamıyor" });
                }

                // TSE imza oluşturma simülasyonu
                string tseSignature = await GenerateTseSignature(tseDevice, request);
                
                if (!string.IsNullOrEmpty(tseSignature))
                {
                    tseDevice.LastSignatureTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("TSE signature created for invoice: {InvoiceNumber}", request.InvoiceNumber);
                    
                    return Ok(new TseSignatureResponse
                    {
                        Success = true,
                        TseSignature = tseSignature,
                        Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                        KassenId = tseDevice.KassenId.ToString(),
                        Message = "TSE imzası başarıyla oluşturuldu"
                    });
                }
                else
                {
                    return BadRequest(new { message = "TSE imzası oluşturulamadı" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE signature creation failed");
                return StatusCode(500, new { message = "TSE imzası oluşturulamadı" });
            }
        }

        // POST: api/tse/disconnect
        [HttpPost("disconnect")]
        public async Task<ActionResult<TseConnectionResponse>> DisconnectTseDevice()
        {
            try
            {
                var tseDevice = await _context.TseDevices
                    .Where(t => t.IsActive && t.IsConnected)
                    .FirstOrDefaultAsync();

                if (tseDevice == null)
                {
                    return BadRequest(new { message = "Bağlı TSE cihazı bulunamadı" });
                }

                tseDevice.IsConnected = false;
                tseDevice.CanCreateInvoices = false;
                tseDevice.ErrorMessage = "TSE cihazı bağlantısı kesildi";
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("TSE device disconnected: {SerialNumber}", tseDevice.SerialNumber);
                
                return Ok(new TseConnectionResponse
                {
                    Success = true,
                    Message = "TSE cihazı başarıyla bağlantısı kesildi"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE disconnection failed");
                return StatusCode(500, new { message = "TSE bağlantısı kesilemedi" });
            }
        }

        // GET: api/tse/devices
        [HttpGet("devices")]
        public async Task<ActionResult<List<TseDevice>>> GetTseDevices()
        {
            try
            {
                var devices = await _context.TseDevices
                    .Where(t => t.IsActive)
                    .OrderByDescending(t => t.LastConnectionTime)
                    .ToListAsync();

                return Ok(devices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TSE devices fetch failed");
                return StatusCode(500, new { message = "TSE cihazları alınamadı" });
            }
        }

        // Private helper methods
        private async Task<bool> SimulateTseConnection(TseDevice device)
        {
            // Gerçek implementasyonda USB bağlantısı kontrol edilir
            await Task.Delay(100); // Simülasyon için kısa bekleme
            
            // VID_04B8&PID_0E15 kontrolü (Epson TSE)
            if (device.VendorId == "VID_04B8" && device.ProductId == "PID_0E15")
            {
                return true; // Epson TSE bağlantısı başarılı
            }
            
            return false;
        }

        private async Task<string> GenerateTseSignature(TseDevice device, TseSignatureRequest request)
        {
            // Gerçek implementasyonda TSE cihazından imza alınır
            await Task.Delay(200); // Simülasyon için kısa bekleme
            
            // Benzersiz TSE imzası oluştur
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string randomPart = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            return $"TSE-{device.SerialNumber}-{timestamp}-{randomPart}";
        }
    }

    // Request/Response Models
    public class TseStatusResponse
    {
        public bool IsConnected { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string CertificateStatus { get; set; } = string.Empty;
        public string MemoryStatus { get; set; } = string.Empty;
        public string LastSignatureTime { get; set; } = string.Empty;
        public bool CanCreateInvoices { get; set; }
        public string? ErrorMessage { get; set; }
        public string KassenId { get; set; } = string.Empty;
        public bool FinanzOnlineEnabled { get; set; }
    }

    public class TseConnectionRequest
    {
        [Required]
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class TseConnectionResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public TseDeviceInfo? DeviceInfo { get; set; }
    }

    public class TseDeviceInfo
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string KassenId { get; set; } = string.Empty;
        public string CertificateStatus { get; set; } = string.Empty;
        public string MemoryStatus { get; set; } = string.Empty;
    }

    public class TseSignatureRequest
    {
        [Required]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        [Required]
        public decimal TotalAmount { get; set; }
        
        [Required]
        public string TaxDetails { get; set; } = string.Empty;
    }

    public class TseSignatureResponse
    {
        public bool Success { get; set; }
        public string TseSignature { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string KassenId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
