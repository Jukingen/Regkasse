using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Verifies backup execution adapter is suitable for production (PgDump with reachable pg_dump binary).
/// </summary>
public sealed class BackupHealthCheck : IHealthCheck
{
    private const string DefaultPgDumpPath = "/usr/bin/pg_dump";

    private readonly IOptionsMonitor<BackupOptions> _backupOptions;

    public BackupHealthCheck(IOptionsMonitor<BackupOptions> backupOptions)
    {
        _backupOptions = backupOptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _backupOptions.CurrentValue;
        var kind = options.ExecutionAdapterKind;

        if (kind is BackupExecutionAdapterKind.ProductionStub or BackupExecutionAdapterKind.Fake)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Backup is using {kind} - not suitable for production"));
        }

        if (kind == BackupExecutionAdapterKind.PgDump)
        {
            var path = string.IsNullOrWhiteSpace(options.PgDumpExecutablePath)
                ? DefaultPgDumpPath
                : options.PgDumpExecutablePath.Trim();

            if (!File.Exists(path))
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy($"pg_dump not found at {path}"));
            }
        }

        return Task.FromResult(
            HealthCheckResult.Healthy("Backup configured for production"));
    }
}
