using System.Diagnostics;
using KasseAPI_Final.Services.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KasseAPI_Final.HealthChecks;

/// <summary>
/// Reports EF Core pending/applied migration posture.
/// Mapped at <c>/health/migrations</c>. Healthy = no pending; Degraded = pending; Unhealthy = DB error.
/// </summary>
public sealed class EfMigrationsHealthCheck : IHealthCheck
{
    public const string Name = "ef-migrations";
    public const string MigrationsTag = "migrations";

    public const int TimeoutMilliseconds = 3000;

    private readonly IServiceScopeFactory _scopeFactory;

    public EfMigrationsHealthCheck(IServiceScopeFactory scopeFactory)
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
            var svc = scope.ServiceProvider.GetRequiredService<IMigrationStatusService>();
            var status = await svc.GetStatusAsync(timeoutCts.Token).ConfigureAwait(false);
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["durationMs"] = sw.ElapsedMilliseconds,
                ["appliedCount"] = status.AppliedCount,
                ["pendingCount"] = status.PendingCount,
                ["latestApplied"] = status.LatestApplied ?? string.Empty,
                ["pending"] = status.Pending.Take(20).ToArray(),
                ["checkedAtUtc"] = status.CheckedAtUtc,
            };

            return status.Status switch
            {
                "Healthy" => HealthCheckResult.Healthy(
                    $"Schema up to date ({status.AppliedCount} applied).", data),
                "Degraded" => HealthCheckResult.Degraded(
                    $"{status.PendingCount} pending migration(s).", data: data),
                _ => HealthCheckResult.Unhealthy(
                    "Unable to determine migration status.", data: data),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Migration health check timed out after {TimeoutMilliseconds}ms.",
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
                $"Migration health check failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["durationMs"] = sw.ElapsedMilliseconds,
                });
        }
    }
}
