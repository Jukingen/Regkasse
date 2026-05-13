using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KasseAPI_Final;

namespace KasseAPI_Final.Services;

/// <summary>
/// Shared NTP sampling path for background sync and admin-triggered manual sync.
/// </summary>
public sealed class NtpSynchronizationCoordinator : INtpSynchronizationCoordinator
{
    private static readonly TimeSpan PerServerTimeout = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INtpTimeSyncStatus _status;
    private readonly ILogger<NtpSynchronizationCoordinator> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptionsMonitor<DevelopmentOptions> _developmentOptions;

    public NtpSynchronizationCoordinator(
        IServiceScopeFactory scopeFactory,
        INtpTimeSyncStatus status,
        ILogger<NtpSynchronizationCoordinator> logger,
        IHostEnvironment hostEnvironment,
        IOptionsMonitor<DevelopmentOptions> developmentOptions)
    {
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _developmentOptions = developmentOptions;
    }

    public async Task<NtpSyncCycleResult> RunSynchronizationCycleAsync(
        NtpSettings settings,
        bool ignoreDisabled,
        CancellationToken cancellationToken)
    {
        if (!ignoreDisabled && !settings.Enabled)
        {
            return new NtpSyncCycleResult
            {
                Ran = false,
                LogicalSuccess = true,
                Message = "NTP auto-sync is disabled."
            };
        }

        if (!OpenApiExportMode.IsEnabled
            && _hostEnvironment.IsDevelopment()
            && _developmentOptions.CurrentValue.SimulateNtpFailure
            && (ignoreDisabled || settings.Enabled))
        {
            var simSyncUtc = DateTime.UtcNow;
            const string simMsg = "Development simulation: NTP failure.";
            _logger.LogWarning("{Message}", simMsg);
            await PersistAndPublishAsync(
                    simSyncUtc,
                    simSyncUtc,
                    default,
                    null,
                    false,
                    simMsg,
                    cancellationToken)
                .ConfigureAwait(false);
            return new NtpSyncCycleResult
            {
                Ran = true,
                LogicalSuccess = false,
                Message = simMsg
            };
        }

        var syncTimeUtc = DateTime.UtcNow;
        var servers = settings.NtpServers?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray()
                      ?? Array.Empty<string>();

        if (servers.Length == 0)
        {
            var msg = "NtpSettings:NtpServers is empty.";
            _logger.LogError("{Message}", msg);
            await PersistAndPublishAsync(
                syncTimeUtc,
                syncTimeUtc,
                default,
                null,
                false,
                msg,
                cancellationToken).ConfigureAwait(false);
            return new NtpSyncCycleResult
            {
                Ran = true,
                LogicalSuccess = false,
                Message = msg
            };
        }

        var samples = new List<(string Host, double Offset, DateTime NtpUtc)>();
        foreach (var host in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var (ok, offset, ntpUtc) = await NtpSnClient.TryQueryOffsetAsync(host, PerServerTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (ok)
                    samples.Add((host, offset, ntpUtc));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "NTP query threw for host {Host}", host);
            }
        }

        if (samples.Count == 0)
        {
            var err = "All configured NTP servers failed or timed out.";
            _logger.LogWarning("{Message}", err);
            await PersistAndPublishAsync(
                syncTimeUtc,
                syncTimeUtc,
                default,
                null,
                false,
                err,
                cancellationToken).ConfigureAwait(false);
            return new NtpSyncCycleResult { Ran = true, LogicalSuccess = false, Message = err };
        }

        var avgOffset = samples.Average(s => s.Offset);
        var avgTicks = samples.Average(s => s.NtpUtc.Ticks);
        var ntpConsensus = new DateTime((long)avgTicks, DateTimeKind.Utc);
        var systemMid = ntpConsensus.AddSeconds(-avgOffset);
        var hostsUsed = string.Join(",", samples.Select(s => s.Host));

        if (Math.Abs(avgOffset) > settings.CriticalOffsetSeconds)
        {
            _logger.LogError(
                "NTP critical clock drift: averageOffsetSeconds={Offset} servers={Servers}",
                avgOffset,
                hostsUsed);
        }
        else if (Math.Abs(avgOffset) > settings.MaxAllowedOffsetSeconds)
        {
            _logger.LogWarning(
                "NTP clock drift warning: averageOffsetSeconds={Offset} servers={Servers}",
                avgOffset,
                hostsUsed);
        }

        await PersistAndPublishAsync(
            syncTimeUtc,
            systemMid,
            ntpConsensus,
            avgOffset,
            true,
            null,
            cancellationToken,
            hostsUsed).ConfigureAwait(false);

        await MirrorDriftToCashRegistersAsync(avgOffset, syncTimeUtc, cancellationToken).ConfigureAwait(false);

        return new NtpSyncCycleResult
        {
            Ran = true,
            LogicalSuccess = true,
            AverageOffsetSeconds = avgOffset,
            Message = "Synchronization cycle completed."
        };
    }

    private async Task MirrorDriftToCashRegistersAsync(
        double offsetSeconds,
        DateTime measuredAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE cash_registers
                SET last_server_time_offset_seconds = {offsetSeconds},
                    last_server_time_drift_at_utc = {measuredAtUtc}
                """,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mirror NTP drift onto cash_registers rows.");
        }
    }

    private async Task PersistAndPublishAsync(
        DateTime syncTimeUtc,
        DateTime systemTimeUtc,
        DateTime ntpTimeUtc,
        double? offsetSeconds,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken,
        string? ntpServersUsed = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var row = new SystemTimeSyncLog
        {
            Id = Guid.NewGuid(),
            SyncTimeUtc = syncTimeUtc,
            SystemTimeUtc = systemTimeUtc,
            NtpTimeUtc = success ? ntpTimeUtc : systemTimeUtc,
            OffsetSeconds = success ? (offsetSeconds ?? 0) : 0,
            NtpServerUsed = ntpServersUsed ?? string.Empty,
            IsSuccess = success,
            ErrorMessage = errorMessage
        };

        db.SystemTimeSyncLogs.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist SystemTimeSyncLog row.");
        }

        _status.RecordSynchronizationAttempt(
            syncTimeUtc,
            systemTimeUtc,
            success ? ntpTimeUtc : null,
            offsetSeconds,
            success,
            errorMessage);
    }
}
