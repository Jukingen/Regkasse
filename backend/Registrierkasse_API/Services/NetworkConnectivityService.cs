using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;

namespace Registrierkasse_API.Services
{
    public interface INetworkConnectivityService
    {
        Task<bool> IsInternetAvailableAsync();
        Task<bool> IsFinanzOnlineAvailableAsync();
        Task<NetworkStatus> GetNetworkStatusAsync();
        Task<bool> TestConnectionAsync(string url);
        void StartMonitoring();
        void StopMonitoring();
        event EventHandler<NetworkStatusChangedEventArgs> NetworkStatusChanged;
    }

    public class NetworkConnectivityService : INetworkConnectivityService, IDisposable
    {
        private readonly ILogger<NetworkConnectivityService> _logger;
        private readonly HttpClient _httpClient;
        private readonly Timer _monitoringTimer;
        private NetworkStatus _currentStatus;
        private bool _isMonitoring;

        public event EventHandler<NetworkStatusChangedEventArgs>? NetworkStatusChanged;

        public NetworkConnectivityService(ILogger<NetworkConnectivityService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _currentStatus = new NetworkStatus { IsInternetAvailable = false, IsFinanzOnlineAvailable = false };
            _monitoringTimer = new Timer(MonitorNetworkStatus, null, Timeout.Infinite, Timeout.Infinite);
            _isMonitoring = false;
        }

        public async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                // DNS sorgusu ile internet bağlantısını kontrol et
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                return reply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Internet bağlantısı kontrolü başarısız");
                return false;
            }
        }

        public async Task<bool> IsFinanzOnlineAvailableAsync()
        {
            try
            {
                var finanzOnlineUrl = Environment.GetEnvironmentVariable("FINANZONLINE_API_URL") ?? "https://finanzonline.bmf.gv.at";
                return await TestConnectionAsync(finanzOnlineUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FinanzOnline bağlantısı kontrolü başarısız");
                return false;
            }
        }

        public async Task<NetworkStatus> GetNetworkStatusAsync()
        {
            var internetAvailable = await IsInternetAvailableAsync();
            var finanzOnlineAvailable = internetAvailable && await IsFinanzOnlineAvailableAsync();

            var newStatus = new NetworkStatus
            {
                IsInternetAvailable = internetAvailable,
                IsFinanzOnlineAvailable = finanzOnlineAvailable,
                LastChecked = DateTime.UtcNow,
                Status = internetAvailable ? (finanzOnlineAvailable ? "FULLY_CONNECTED" : "INTERNET_ONLY") : "DISCONNECTED"
            };

            // Durum değişikliği varsa event tetikle
            if (_currentStatus.Status != newStatus.Status)
            {
                var oldStatus = _currentStatus;
                _currentStatus = newStatus;
                NetworkStatusChanged?.Invoke(this, new NetworkStatusChangedEventArgs(oldStatus, newStatus));
                
                _logger.LogInformation("Network durumu değişti: {OldStatus} -> {NewStatus}", 
                    oldStatus.Status, newStatus.Status);
            }

            return newStatus;
        }

        public async Task<bool> TestConnectionAsync(string url)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.GetAsync(url, cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Bağlantı testi başarısız: {Url}", url);
                return false;
            }
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitoringTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30)); // Her 30 saniyede bir kontrol
            _logger.LogInformation("Network monitoring başlatıldı");
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _logger.LogInformation("Network monitoring durduruldu");
        }

        private async void MonitorNetworkStatus(object? state)
        {
            try
            {
                await GetNetworkStatusAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Network monitoring hatası");
            }
        }

        public void Dispose()
        {
            _monitoringTimer?.Dispose();
        }
    }

    public class NetworkStatus
    {
        public bool IsInternetAvailable { get; set; }
        public bool IsFinanzOnlineAvailable { get; set; }
        public DateTime LastChecked { get; set; }
        public string Status { get; set; } = "UNKNOWN"; // DISCONNECTED, INTERNET_ONLY, FULLY_CONNECTED
    }

    public class NetworkStatusChangedEventArgs : EventArgs
    {
        public NetworkStatus OldStatus { get; }
        public NetworkStatus NewStatus { get; }

        public NetworkStatusChangedEventArgs(NetworkStatus oldStatus, NetworkStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }
} 