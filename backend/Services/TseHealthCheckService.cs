using System;
using System.Threading;
using System.Threading.Tasks;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodically probes the TSE layer and updates <see cref="TseHealthStateStore"/> (in-memory).
/// </summary>
public sealed class TseHealthCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TseHealthStateStore _state;
    private readonly ILogger<TseHealthCheckService> _logger;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;

    public TseHealthCheckService(
        IServiceScopeFactory scopeFactory,
        TseHealthStateStore state,
        ILogger<TseHealthCheckService> logger,
        IOptionsMonitor<TseOptions> tseOptions)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
        _tseOptions = tseOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Soft / Off modes: device probes are not meaningful; keep store "Online" so payment policy stays unchanged.
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _tseOptions.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Clamp(opts.HealthCheckIntervalSeconds, 5, 600));

            if (opts.IsOff || opts.UseSoftTseWhenNoDevice)
            {
                _state.ApplyProbeResult(pingSucceeded: true, errorSafe: null);
                try
                {
                    await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                continue;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var tse = scope.ServiceProvider.GetRequiredService<ITseService>();
                var status = await tse.GetDeviceStatusAsync().ConfigureAwait(false);
                var ok = status.IsConnected && status.IsReady;
                var err = ok
                    ? null
                    : string.IsNullOrWhiteSpace(status.ErrorMessage)
                        ? $"TSE not operational (Status={status.Status}, Connected={status.IsConnected}, Ready={status.IsReady})"
                        : status.ErrorMessage;

                _state.ApplyProbeResult(ok, err);
                if (!ok)
                {
                    _logger.LogWarning(
                        "TSE health probe failed: Connected={Connected} Ready={Ready} Status={DeviceStatus} Error={Error}",
                        status.IsConnected,
                        status.IsReady,
                        status.Status,
                        err);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "TSE health probe exception");
                _state.ApplyProbeResult(
                    false,
                    ex.Message.Length > 400 ? ex.Message[..400] : ex.Message);
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
