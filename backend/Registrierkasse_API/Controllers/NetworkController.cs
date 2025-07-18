using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Registrierkasse_API.Services;
using System.Threading.Tasks;

namespace Registrierkasse_API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NetworkController : ControllerBase
    {
        private readonly INetworkConnectivityService _networkService;
        private readonly ILogger<NetworkController> _logger;

        public NetworkController(INetworkConnectivityService networkService, ILogger<NetworkController> logger)
        {
            _networkService = networkService;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetNetworkStatus()
        {
            try
            {
                var status = await _networkService.GetNetworkStatusAsync();
                
                var response = new
                {
                    isInternetAvailable = status.IsInternetAvailable,
                    isFinanzOnlineAvailable = status.IsFinanzOnlineAvailable,
                    lastChecked = status.LastChecked,
                    status = status.Status,
                    canProcessInvoices = status.IsInternetAvailable, // TSE bağlı ise fiş kesebilir
                    canSubmitToFinanzOnline = status.IsFinanzOnlineAvailable,
                    recommendations = GetRecommendations(status)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Network durumu alma hatası");
                return StatusCode(500, new { error = "Network durumu alınamadı" });
            }
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestConnection([FromBody] TestConnectionRequest request)
        {
            try
            {
                var isAvailable = await _networkService.TestConnectionAsync(request.Url);
                
                return Ok(new
                {
                    url = request.Url,
                    isAvailable = isAvailable,
                    message = isAvailable ? "Bağlantı başarılı" : "Bağlantı başarısız"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bağlantı testi hatası");
                return StatusCode(500, new { error = "Bağlantı testi başarısız" });
            }
        }

        [HttpPost("monitoring/start")]
        [Authorize(Roles = "Administrator")]
        public IActionResult StartMonitoring()
        {
            try
            {
                _networkService.StartMonitoring();
                return Ok(new { message = "Network monitoring başlatıldı" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitoring başlatma hatası");
                return StatusCode(500, new { error = "Monitoring başlatılamadı" });
            }
        }

        [HttpPost("monitoring/stop")]
        [Authorize(Roles = "Administrator")]
        public IActionResult StopMonitoring()
        {
            try
            {
                _networkService.StopMonitoring();
                return Ok(new { message = "Network monitoring durduruldu" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitoring durdurma hatası");
                return StatusCode(500, new { error = "Monitoring durdurulamadı" });
            }
        }

        [HttpGet("health")]
        public async Task<IActionResult> GetHealthCheck()
        {
            try
            {
                var internetAvailable = await _networkService.IsInternetAvailableAsync();
                var finanzOnlineAvailable = await _networkService.IsFinanzOnlineAvailableAsync();

                var healthStatus = new
                {
                    timestamp = DateTime.UtcNow,
                    internet = new
                    {
                        available = internetAvailable,
                        status = internetAvailable ? "HEALTHY" : "UNHEALTHY"
                    },
                    finanzOnline = new
                    {
                        available = finanzOnlineAvailable,
                        status = finanzOnlineAvailable ? "HEALTHY" : "UNHEALTHY"
                    },
                    overall = new
                    {
                        status = internetAvailable ? "OPERATIONAL" : "DEGRADED",
                        message = GetHealthMessage(internetAvailable, finanzOnlineAvailable)
                    }
                };

                return Ok(healthStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check hatası");
                return StatusCode(500, new { error = "Health check başarısız" });
            }
        }

        private string[] GetRecommendations(NetworkStatus status)
        {
            var recommendations = new List<string>();

            if (!status.IsInternetAvailable)
            {
                recommendations.Add("İnternet bağlantısını kontrol edin");
                recommendations.Add("TSE cihazının bağlı olduğundan emin olun");
                recommendations.Add("Local fiş kesme işlemlerine devam edebilirsiniz");
            }
            else if (!status.IsFinanzOnlineAvailable)
            {
                recommendations.Add("FinanzOnline bağlantısını kontrol edin");
                recommendations.Add("Faturalar local'de saklanacak, bağlantı geldiğinde gönderilecek");
                recommendations.Add("Normal fiş kesme işlemlerine devam edebilirsiniz");
            }
            else
            {
                recommendations.Add("Tüm bağlantılar normal çalışıyor");
                recommendations.Add("Tüm özellikler kullanılabilir");
            }

            return recommendations.ToArray();
        }

        private string GetHealthMessage(bool internetAvailable, bool finanzOnlineAvailable)
        {
            if (!internetAvailable)
                return "İnternet bağlantısı yok - TSE bağlı ise local çalışma mümkün";
            
            if (!finanzOnlineAvailable)
                return "FinanzOnline bağlantısı yok - Faturalar local'de saklanacak";
            
            return "Tüm sistemler normal çalışıyor";
        }
    }

    public class TestConnectionRequest
    {
        public string Url { get; set; } = string.Empty;
    }
} 