using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Periodically probes the TSE layer and updates <see cref="TseHealthStateStore"/> (in-memory).
/// </summary>
public sealed class TseHealthCheckService : BackgroundService
{
    private static int _developmentTseBypassLogged;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TseHealthStateStore _state;
    private readonly ILogger<TseHealthCheckService> _logger;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly IDevelopmentModeService _developmentModeService;

    public TseHealthCheckService(
        IServiceScopeFactory scopeFactory,
        TseHealthStateStore state,
        ILogger<TseHealthCheckService> logger,
        IOptionsMonitor<TseOptions> tseOptions,
        IDevelopmentModeService developmentModeService)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
        _tseOptions = tseOptions;
        _developmentModeService = developmentModeService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Soft / Off modes: device probes are not meaningful; keep store "Online" so payment policy stays unchanged.
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _tseOptions.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Clamp(opts.HealthCheckIntervalSeconds, 5, 600));

            if (_developmentModeService.ShouldBypassTseCheck())
            {
                if (Interlocked.Exchange(ref _developmentTseBypassLogged, 1) == 0)
                    _logger.LogWarning("Development mode active: {BypassType} bypassed", "TSE");
                var beforeDev = _state.Snapshot;
                _state.ApplyProbeResult(pingSucceeded: true, errorSafe: null);
                await TryPersistHealthChangeAuditAsync(beforeDev, _state.Snapshot, stoppingToken).ConfigureAwait(false);
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

            if (opts.IsOff || opts.UseSoftTseWhenNoDevice)
            {
                var beforeSoft = _state.Snapshot;
                _state.ApplyProbeResult(pingSucceeded: true, errorSafe: null);
                await TryPersistHealthChangeAuditAsync(beforeSoft, _state.Snapshot, stoppingToken).ConfigureAwait(false);
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
                var before = _state.Snapshot;
                var status = await tse.GetDeviceStatusAsync().ConfigureAwait(false);
                var ok = status.IsConnected && status.IsReady;
                var err = ok
                    ? null
                    : string.IsNullOrWhiteSpace(status.ErrorMessage)
                        ? $"TSE not operational (Status={status.Status}, Connected={status.IsConnected}, Ready={status.IsReady})"
                        : status.ErrorMessage;

                _state.ApplyProbeResult(ok, err);
                await TryPersistHealthChangeAuditAsync(before, _state.Snapshot, stoppingToken).ConfigureAwait(false);
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
                var before = _state.Snapshot;
                _state.ApplyProbeResult(
                    false,
                    ex.Message.Length > 400 ? ex.Message[..400] : ex.Message);
                await TryPersistHealthChangeAuditAsync(before, _state.Snapshot, stoppingToken).ConfigureAwait(false);
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

    private async Task TryPersistHealthChangeAuditAsync(
        TseHealthSnapshot previous,
        TseHealthSnapshot current,
        CancellationToken cancellationToken)
    {
        if (previous.Status == current.Status)
            return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var reason = current.Status == TseOperationalHealth.Degraded ? current.LastErrorMessageSafe : null;
            db.TseHealthAuditLogs.Add(new TseHealthAuditLog
            {
                Id = Guid.NewGuid(),
                TimestampUtc = current.LastCheckUtc ?? DateTime.UtcNow,
                OldStatus = previous.Status,
                NewStatus = current.Status,
                ConsecutiveFailures = current.ConsecutiveFailures,
                ReasonSafe = reason
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(
                "TSE health changed: {OldStatus} → {NewStatus}, Failures: {FailureCount}",
                previous.Status,
                current.Status,
                current.ConsecutiveFailures);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TSE health audit persist failed (Old={Old}, New={New})",
                previous.Status,
                current.Status);
        }
    }
}
