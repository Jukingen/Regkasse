using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Hosted;

/// <summary>Periodically auto-closes cash registers / cashier shifts left open beyond <see cref="ShiftAutoCloseOptions.MaxOpenDurationHours"/> (inactivity).</summary>
public sealed class ShiftAutoCloseHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ShiftAutoCloseOptions _options;
    private readonly ILogger<ShiftAutoCloseHostedService> _logger;

    public ShiftAutoCloseHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ShiftAutoCloseOptions> options,
        ILogger<ShiftAutoCloseHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(5, _options.CheckIntervalMinutes);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (!_options.Enabled)
                continue;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
                tenantAccessor.TenantId = null;

                var autoClose = scope.ServiceProvider.GetRequiredService<IShiftAutoCloseService>();
                var closed = await autoClose.CloseStaleOpenRegistersAsync(stoppingToken).ConfigureAwait(false);

                if (closed > 0)
                    _logger.LogWarning("Shift auto-close closed {Count} stale register(s).", closed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Shift auto-close hosted service iteration failed.");
            }
        }
    }
}
