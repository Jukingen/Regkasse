using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Registrierkasse_API.Services
{
    public class BackgroundInvoiceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundInvoiceService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // 5 dakikada bir kontrol

        public BackgroundInvoiceService(
            IServiceProvider serviceProvider,
            ILogger<BackgroundInvoiceService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Invoice Service başlatıldı");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingInvoicesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background invoice processing hatası");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Background Invoice Service durduruldu");
        }

        private async Task ProcessPendingInvoicesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var pendingService = scope.ServiceProvider.GetRequiredService<IPendingInvoicesService>();
            var networkService = scope.ServiceProvider.GetRequiredService<INetworkConnectivityService>();

            try
            {
                // Network durumunu kontrol et
                var networkStatus = await networkService.GetNetworkStatusAsync();
                if (!networkStatus.IsFinanzOnlineAvailable)
                {
                    _logger.LogDebug("FinanzOnline bağlantısı yok, bekleyen faturalar işlenmedi");
                    return;
                }

                // Bekleyen fatura sayısını kontrol et
                var pendingCount = await pendingService.GetPendingCountAsync();
                if (pendingCount == 0)
                {
                    _logger.LogDebug("Gönderilecek bekleyen fatura yok");
                    return;
                }

                _logger.LogInformation("{Count} adet bekleyen fatura işleniyor", pendingCount);

                // Bekleyen faturaları gönder
                var success = await pendingService.SubmitPendingInvoicesAsync();
                if (success)
                {
                    _logger.LogInformation("Bekleyen faturalar başarıyla gönderildi");
                }
                else
                {
                    _logger.LogWarning("Bazı bekleyen faturalar gönderilemedi");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bekleyen faturalar işleme hatası");
            }
        }
    }
} 