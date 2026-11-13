using System.Diagnostics;
using KasseAPI_Final.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Lightweight PostgreSQL reachability check for readiness / dependency probes.
/// Uses a short timeout so frequent orchestrator polls do not pile up.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    public const string Name = "database";
    public const string ReadyTag = "ready";
    public const string DepsTag = "deps";

    /// <summary>Hard cap for DB probe duration (milliseconds).</summary>
    public const int TimeoutMilliseconds = 2000;

    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeoutMilliseconds);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await db.Database.CanConnectAsync(timeoutCts.Token).ConfigureAwait(false);
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["durationMs"] = sw.ElapsedMilliseconds,
                ["timeoutMs"] = TimeoutMilliseconds,
            };

            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.", data)
                : HealthCheckResult.Unhealthy("Database is not reachable.", data: data);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Database health check timed out after {TimeoutMilliseconds}ms.",
                data: new Dictionary<string, object>
                {
                    ["durationMs"] = sw.ElapsedMilliseconds,
                    ["timeoutMs"] = TimeoutMilliseconds,
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Database health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["durationMs"] = sw.ElapsedMilliseconds,
                });
        }
    }
}
