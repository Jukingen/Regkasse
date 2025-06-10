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
    public class HardwareController : ControllerBase
    {
        private readonly ITseHardwareService _tseHardwareService;
        private readonly IPrinterService _printerService;
        private readonly ILogger<HardwareController> _logger;

        public HardwareController(
            ITseHardwareService tseHardwareService,
            IPrinterService printerService,
            ILogger<HardwareController> logger)
        {
            _tseHardwareService = tseHardwareService;
            _printerService = printerService;
            _logger = logger;
        }

        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            try
            {
                var config = new
                {
                    TseDeviceId = Environment.GetEnvironmentVariable("TSE_DEVICE_ID") ?? "",
                    TseSerialNumber = Environment.GetEnvironmentVariable("TSE_SERIAL_NUMBER") ?? "",
                    PrinterName = Environment.GetEnvironmentVariable("PRINTER_NAME") ?? "",
                    PrinterPort = Environment.GetEnvironmentVariable("PRINTER_PORT") ?? "",
                    AutoConnect = bool.Parse(Environment.GetEnvironmentVariable("AUTO_CONNECT") ?? "false"),
                    ConnectionTimeout = int.Parse(Environment.GetEnvironmentVariable("CONNECTION_TIMEOUT") ?? "30"),
                    RetryAttempts = int.Parse(Environment.GetEnvironmentVariable("RETRY_ATTEMPTS") ?? "3")
                };

                return Ok(config);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Donanım konfigürasyonu alma hatası");
                return StatusCode(500, new { message = "Konfigürasyon alınamadı" });
            }
        }

        [HttpPost("config")]
        public async Task<IActionResult> SaveConfig([FromBody] HardwareConfigModel config)
        {
            try
            {
                // Konfigürasyonu environment variable olarak kaydet
                Environment.SetEnvironmentVariable("TSE_DEVICE_ID", config.TseDeviceId);
                Environment.SetEnvironmentVariable("TSE_SERIAL_NUMBER", config.TseSerialNumber);
                Environment.SetEnvironmentVariable("PRINTER_NAME", config.PrinterName);
                Environment.SetEnvironmentVariable("PRINTER_PORT", config.PrinterPort);
                Environment.SetEnvironmentVariable("AUTO_CONNECT", config.AutoConnect.ToString());
                Environment.SetEnvironmentVariable("CONNECTION_TIMEOUT", config.ConnectionTimeout.ToString());
                Environment.SetEnvironmentVariable("RETRY_ATTEMPTS", config.RetryAttempts.ToString());

                _logger.LogInformation("Donanım konfigürasyonu kaydedildi");
                return Ok(new { message = "Konfigürasyon kaydedildi" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Donanım konfigürasyonu kaydetme hatası");
                return StatusCode(500, new { message = "Konfigürasyon kaydedilemedi" });
            }
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var tseConnected = await _tseHardwareService.IsConnectedAsync();
                var printerConnected = await _printerService.IsConnectedAsync();
                var tseSerialNumber = await _tseHardwareService.GetSerialNumberAsync();
                var printerStatus = await _printerService.GetStatusAsync();

                var status = new
                {
                    TseConnected = tseConnected,
                    PrinterConnected = printerConnected,
                    TseSerialNumber = tseSerialNumber,
                    PrinterName = printerStatus.PrinterName,
                    LastConnectionTime = System.DateTime.UtcNow.ToString()
                };

                return Ok(status);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Donanım durumu alma hatası");
                return StatusCode(500, new { message = "Durum bilgisi alınamadı" });
            }
        }

        [HttpGet("printers")]
        public async Task<IActionResult> GetAvailablePrinters()
        {
            try
            {
                var printers = await _printerService.GetAvailablePrintersAsync();
                return Ok(printers);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Yazıcı listesi alma hatası");
                return StatusCode(500, new { message = "Yazıcı listesi alınamadı" });
            }
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var tseConnected = await _tseHardwareService.IsConnectedAsync();
                var printerConnected = await _printerService.IsConnectedAsync();

                if (tseConnected && printerConnected)
                {
                    return Ok(new { message = "Tüm donanım bağlantıları başarılı" });
                }
                else
                {
                    return BadRequest(new { message = "Bazı donanım bağlantıları başarısız" });
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Bağlantı testi hatası");
                return StatusCode(500, new { message = "Bağlantı testi başarısız" });
            }
        }

        [HttpPost("connect")]
        public async Task<IActionResult> Connect()
        {
            try
            {
                var tseConnected = await _tseHardwareService.ConnectAsync();
                var printerConnected = await _printerService.ConnectAsync();

                if (tseConnected && printerConnected)
                {
                    _logger.LogInformation("Donanım bağlantıları başarılı");
                    return Ok(new { message = "Donanım başarıyla bağlandı" });
                }
                else
                {
                    return BadRequest(new { message = "Donanım bağlantısı başarısız" });
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Donanım bağlantı hatası");
                return StatusCode(500, new { message = "Donanım bağlantısı başarısız" });
            }
        }

        [HttpPost("disconnect")]
        public async Task<IActionResult> Disconnect()
        {
            try
            {
                await _tseHardwareService.DisconnectAsync();
                await _printerService.DisconnectAsync();

                _logger.LogInformation("Donanım bağlantıları kesildi");
                return Ok(new { message = "Donanım bağlantıları kesildi" });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Donanım bağlantısını kesme hatası");
                return StatusCode(500, new { message = "Donanım bağlantısı kesilemedi" });
            }
        }
    }

    public class HardwareConfigModel
    {
        public string TseDeviceId { get; set; } = string.Empty;
        public string TseSerialNumber { get; set; } = string.Empty;
        public string PrinterName { get; set; } = string.Empty;
        public string PrinterPort { get; set; } = string.Empty;
        public bool AutoConnect { get; set; }
        public int ConnectionTimeout { get; set; }
        public int RetryAttempts { get; set; }
    }
} 