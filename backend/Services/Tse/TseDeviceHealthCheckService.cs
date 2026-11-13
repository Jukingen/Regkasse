using System.Diagnostics;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Evaluates and persists per-device TSE health. Uses device row state + optional
/// <see cref="ITseProvider.IsReadyAsync"/> when the device is process-relevant
/// (no vendor <c>GetHealthAsync</c> exists on <see cref="ITseProvider"/> yet).
/// </summary>
public sealed class TseDeviceHealthCheckService : ITseDeviceHealthCheckService
{
    private readonly AppDbContext _db;
    private readonly ITseProvider _tseProvider;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;
    private readonly ITseHealthTrendService _healthTrend;
    private readonly ITseSimulatorStateStore _simulatorState;
    private readonly ILogger<TseDeviceHealthCheckService> _logger;

    public TseDeviceHealthCheckService(
        AppDbContext db,
        ITseProvider tseProvider,
        IOptionsMonitor<TseOptions> tseOptions,
        ITseHealthTrendService healthTrend,
        ITseSimulatorStateStore simulatorState,
        ILogger<TseDeviceHealthCheckService> logger)
    {
        _db = db;
        _tseProvider = tseProvider;
        _tseOptions = tseOptions;
        _healthTrend = healthTrend;
        _simulatorState = simulatorState;
        _logger = logger;
    }

    public async Task<TseHealthResult> CheckHealthAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
            return TseHealthResult.Fail("Device id is required.");

        var device = await _db.TseDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
            return TseHealthResult.Fail(deviceId, "Device not found", TseHealthStatus.Offline);

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await EvaluateAsync(device, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            result.ResponseTimeMs = (int)Math.Min(sw.ElapsedMilliseconds, int.MaxValue);

            device.HealthStatus = result.Status;
            device.HealthScore = result.HealthScore;
            device.LastHealthCheck = result.CheckedAt;
            device.HealthMessage = Truncate(result.Message, 1000);
            device.UpdatedAt = DateTime.UtcNow;

            if (result.Status is TseHealthStatus.Expired)
                device.CertificateStatus = "EXPIRED";
            else if (result.Status is TseHealthStatus.Revoked)
                device.CertificateStatus = "REVOKED";

            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _healthTrend.TryRecordSampleAsync(
                    device,
                    result.HealthScore,
                    result.Status,
                    result.Message,
                    result.CheckedAt,
                    result.ResponseTimeMs,
                    cancellationToken)
                .ConfigureAwait(false);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Health check failed for TSE device {DeviceId}", deviceId);

            device.HealthStatus = TseHealthStatus.Offline;
            device.HealthScore = 0;
            device.LastHealthCheck = DateTime.UtcNow;
            device.HealthMessage = Truncate($"Health check failed: {ex.Message}", 1000);
            device.UpdatedAt = DateTime.UtcNow;
            var responseMs = (int)Math.Min(sw.ElapsedMilliseconds, int.MaxValue);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await _healthTrend.TryRecordSampleAsync(
                        device,
                        0,
                        TseHealthStatus.Offline,
                        device.HealthMessage,
                        device.LastHealthCheck.Value,
                        responseMs,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "Failed to persist offline health for device {DeviceId}", deviceId);
            }

            var fail = TseHealthResult.Fail(deviceId, $"Health check failed: {ex.Message}");
            fail.ResponseTimeMs = responseMs;
            return fail;
        }
    }

    public async Task<IReadOnlyList<TseHealthResult>> CheckAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        var ids = await _db.TseDevices
            .AsNoTracking()
            .Where(d => d.IsActive)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<TseHealthResult>(ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await CheckHealthAsync(id, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<bool> IsDeviceOperationalAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var result = await CheckHealthAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return result.IsHealthy
               && result.Status is TseHealthStatus.Healthy or TseHealthStatus.Degraded;
    }

    private async Task<TseHealthResult> EvaluateAsync(TseDevice device, CancellationToken cancellationToken)
    {
        var simulatedLatencyMs = _simulatorState.GetLatencyMs(device.Id);
        if (simulatedLatencyMs > 0)
        {
            await Task.Delay(simulatedLatencyMs, cancellationToken).ConfigureAwait(false);
        }

        var opts = _tseOptions.CurrentValue;
        var checkedAt = DateTime.UtcNow;
        var score = 100;
        var messages = new List<string>();

        if (simulatedLatencyMs > 0)
            messages.Add($"SIM latency {simulatedLatencyMs}ms");

        if (!device.IsActive)
        {
            return new TseHealthResult
            {
                DeviceId = device.Id,
                IsHealthy = false,
                HealthScore = 0,
                Status = TseHealthStatus.Unhealthy,
                Message = "Device is inactive",
                CheckedAt = checkedAt,
            };
        }

        if (string.Equals(device.CertificateStatus, "REVOKED", StringComparison.OrdinalIgnoreCase))
        {
            return new TseHealthResult
            {
                DeviceId = device.Id,
                IsHealthy = false,
                HealthScore = 0,
                Status = TseHealthStatus.Revoked,
                Message = "Certificate revoked",
                CheckedAt = checkedAt,
            };
        }

        var expiredByStatus = string.Equals(device.CertificateStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase);
        var expiredByDate = device.ExpiresAt.HasValue && device.ExpiresAt.Value <= checkedAt;
        if (expiredByStatus || expiredByDate)
        {
            return new TseHealthResult
            {
                DeviceId = device.Id,
                IsHealthy = false,
                HealthScore = 5,
                Status = TseHealthStatus.Expired,
                Message = expiredByDate
                    ? $"Certificate expired at {device.ExpiresAt:u}"
                    : "Certificate status EXPIRED",
                CheckedAt = checkedAt,
            };
        }

        if (!device.IsConnected)
        {
            score -= 40;
            messages.Add("Not connected");
        }

        if (!device.CanCreateInvoices)
        {
            score -= 30;
            messages.Add("Cannot create invoices");
        }

        if (string.Equals(device.MemoryStatus, "FULL", StringComparison.OrdinalIgnoreCase))
        {
            score -= 35;
            messages.Add("Memory full");
        }
        else if (string.Equals(device.MemoryStatus, "LOW", StringComparison.OrdinalIgnoreCase))
        {
            score -= 15;
            messages.Add("Memory low");
        }

        if (!string.IsNullOrWhiteSpace(device.ErrorMessage))
        {
            score -= 10;
            messages.Add("Device error present");
        }

        // Soft / Off modes: treat provider readiness as always OK for scoring.
        if (!opts.IsOff && !opts.UseSoftTseWhenNoDevice && !opts.IsFakeSigningMode)
        {
            var providerReady = await _tseProvider.IsReadyAsync(cancellationToken).ConfigureAwait(false);
            if (!providerReady && (device.IsPrimary || device.IsFailoverActive))
            {
                score -= 45;
                messages.Add("TSE provider not ready");
            }
        }

        score = Math.Clamp(score, 0, 100);
        var status = MapHealthStatus(score, healthyMin: opts.FailoverHealthyMinScore, degradedMin: opts.FailoverDegradedMinScore);
        var isHealthy = status is TseHealthStatus.Healthy or TseHealthStatus.Degraded;

        return new TseHealthResult
        {
            DeviceId = device.Id,
            IsHealthy = isHealthy,
            HealthScore = score,
            Status = status,
            Message = messages.Count == 0 ? "OK" : string.Join("; ", messages),
            CheckedAt = checkedAt,
        };
    }

    internal static TseHealthStatus MapHealthStatus(int score, int healthyMin = 80, int degradedMin = 50)
    {
        if (score >= healthyMin)
            return TseHealthStatus.Healthy;
        if (score >= degradedMin)
            return TseHealthStatus.Degraded;
        if (score > 0)
            return TseHealthStatus.Unhealthy;
        return TseHealthStatus.Offline;
    }

    private static string Truncate(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }
}
