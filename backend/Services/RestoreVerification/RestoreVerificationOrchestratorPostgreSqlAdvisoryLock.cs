using System.Diagnostics;
using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Backup worker’dan bağımsız <c>pg_try_advisory_lock(int,int)</c> çifti; <c>Pooling=false</c> bağlantı.
/// </summary>
public sealed class RestoreVerificationOrchestratorPostgreSqlAdvisoryLock : IRestoreVerificationOrchestratorDistributedLock
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<RestoreVerificationOptions> _options;
    private readonly IRestoreVerificationOrchestratorMetrics _metrics;
    private readonly ILogger<RestoreVerificationOrchestratorPostgreSqlAdvisoryLock> _logger;

    public RestoreVerificationOrchestratorPostgreSqlAdvisoryLock(
        IConfiguration configuration,
        IOptionsMonitor<RestoreVerificationOptions> options,
        IRestoreVerificationOrchestratorMetrics metrics,
        ILogger<RestoreVerificationOrchestratorPostgreSqlAdvisoryLock> logger)
    {
        _configuration = configuration;
        _options = options;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<(RestoreVerificationOrchestratorGateAttempt Attempt, IAsyncDisposable? Lease)> TryEnterExclusiveAsync(
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.OrchestratorDistributedLockEnabled)
        {
            _metrics.RecordGateOutcome("disabled_bypass");
            return (RestoreVerificationOrchestratorGateAttempt.DisabledBypass, NoopLease.Instance);
        }

        var rawCs = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(rawCs))
        {
            _logger.LogError("Restore verification distributed lock: DefaultConnection missing.");
            _metrics.RecordGateOutcome("config_missing_connection");
            return (RestoreVerificationOrchestratorGateAttempt.ConnectionFailed, null);
        }

        NpgsqlConnection conn;
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(rawCs.Trim()) { Pooling = false };
            conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore verification distributed lock: failed to open non-pooled connection.");
            _metrics.RecordGateOutcome("connection_open_failed");
            return (RestoreVerificationOrchestratorGateAttempt.ConnectionFailed, null);
        }

        var k1 = opts.OrchestratorAdvisoryLockKey1;
        var k2 = opts.OrchestratorAdvisoryLockKey2;

        try
        {
            await using (var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(@k1, @k2)", conn))
            {
                cmd.Parameters.AddWithValue("k1", k1);
                cmd.Parameters.AddWithValue("k2", k2);
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                var got = scalar is true;
                if (!got)
                {
                    await conn.DisposeAsync();
                    _metrics.RecordGateOutcome("contended");
                    _logger.LogInformation(
                        "Restore verification: advisory lock contended (keys {K1},{K2}); skipping tick.",
                        k1,
                        k2);
                    return (RestoreVerificationOrchestratorGateAttempt.ContendedElsewhere, null);
                }
            }

            _metrics.RecordGateOutcome("acquired");
            _logger.LogDebug(
                "Restore verification: acquired PostgreSQL advisory lock (keys {K1},{K2}).",
                k1,
                k2);

            var sw = Stopwatch.StartNew();
            var lease = new AdvisoryLockLease(conn, k1, k2, sw, _logger, _metrics);
            return (RestoreVerificationOrchestratorGateAttempt.AcquiredLease, lease);
        }
        catch (Exception ex)
        {
            await conn.DisposeAsync();
            _logger.LogError(ex, "Restore verification distributed lock: pg_try_advisory_lock failed.");
            _metrics.RecordGateOutcome("try_lock_failed");
            return (RestoreVerificationOrchestratorGateAttempt.ConnectionFailed, null);
        }
    }

    private sealed class AdvisoryLockLease : IAsyncDisposable
    {
        private readonly NpgsqlConnection _conn;
        private readonly int _k1;
        private readonly int _k2;
        private readonly Stopwatch _sw;
        private readonly ILogger _logger;
        private readonly IRestoreVerificationOrchestratorMetrics _metrics;

        public AdvisoryLockLease(
            NpgsqlConnection conn,
            int k1,
            int k2,
            Stopwatch sw,
            ILogger logger,
            IRestoreVerificationOrchestratorMetrics metrics)
        {
            _conn = conn;
            _k1 = k1;
            _k2 = k2;
            _sw = sw;
            _logger = logger;
            _metrics = metrics;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock(@k1, @k2)", _conn);
                cmd.Parameters.AddWithValue("k1", _k1);
                cmd.Parameters.AddWithValue("k2", _k2);
                var unlocked = await cmd.ExecuteScalarAsync();
                if (unlocked is not true)
                {
                    _logger.LogWarning(
                        "Restore verification: pg_advisory_unlock returned false for keys {K1},{K2}; closing connection anyway.",
                        _k1,
                        _k2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Restore verification: pg_advisory_unlock threw; connection will be disposed.");
            }
            finally
            {
                _sw.Stop();
                _metrics.ObserveLockHoldSeconds(_sw.Elapsed.TotalSeconds);
                await _conn.DisposeAsync();
            }
        }
    }

    private sealed class NoopLease : IAsyncDisposable
    {
        public static readonly NoopLease Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
