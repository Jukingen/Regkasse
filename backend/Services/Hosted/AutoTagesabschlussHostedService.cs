using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>
/// Fallback worker: if the cashier did not perform Tagesabschluss by the tenant-configured
/// Europe/Vienna time, create a TSE-signed Daily closing for the previous business day.
/// </summary>
public sealed class AutoTagesabschlussHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AutoTagesabschlussOptions> _options;
    private readonly ILogger<AutoTagesabschlussHostedService> _logger;

    public AutoTagesabschlussHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AutoTagesabschlussOptions> options,
        ILogger<AutoTagesabschlussHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (OpenApiExportMode.IsEnabled)
            return;

        // Catch up immediately when the process starts after the Vienna cutoff (or with older open days).
        await RunIterationAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var opt = _options.CurrentValue;
                var intervalMinutes = Math.Max(5, opt.CheckIntervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);
                await RunIterationAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Automatic Tagesabschluss hosted service iteration failed.");
            }
        }
    }

    private async Task RunIterationAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
            return;

        var opt = _options.CurrentValue;
        if (!opt.Enabled)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
            tenantAccessor.TenantId = null;

            var autoClose = scope.ServiceProvider.GetRequiredService<IAutoTagesabschlussService>();
            var closed = await autoClose.RunFallbackAsync(cancellationToken: stoppingToken)
                .ConfigureAwait(false);

            if (closed > 0)
                _logger.LogWarning("Automatic Tagesabschluss hosted service created {Count} closing(s).", closed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Automatic Tagesabschluss hosted service iteration failed.");
        }
    }
}
