using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Uzun süren orchestrator işleri sırasında ayrı scope ile lease/heartbeat yenileme (ana EF context ile çakışmaz).
/// </summary>
public static class RunLeaseHeartbeatHelper
{
    /// <summary>İlk heartbeat: Running geçişinde SaveChanges öncesi entity üzerinde çağrılır.</summary>
    public static void StampInitialLease<T>(T entity, DateTime utcNow, TimeSpan leaseTimeout)
        where T : IRunLeaseColumns
    {
        entity.LastHeartbeatAtUtc = utcNow;
        entity.LeaseExpiresAtUtc = utcNow + leaseTimeout;
    }

    public static async Task RunWithBackupHeartbeatAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> heartbeatInterval,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        Func<Task> work,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var hbCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = BackupHeartbeatLoopAsync(scopeFactory, heartbeatInterval, leaseTimeout, runId, logger, hbCts.Token);
        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            hbCts.Cancel();
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
        }
    }

    public static async Task RunWithRestoreHeartbeatAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> heartbeatInterval,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        Func<Task> work,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var hbCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = RestoreHeartbeatLoopAsync(scopeFactory, heartbeatInterval, leaseTimeout, runId, logger, hbCts.Token);
        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            hbCts.Cancel();
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task BackupHeartbeatLoopAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> heartbeatInterval,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var delay = heartbeatInterval();
                if (delay <= TimeSpan.Zero)
                    delay = TimeSpan.FromSeconds(30);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                await TryRenewBackupLeaseAsync(scopeFactory, leaseTimeout, runId, logger, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static async Task RestoreHeartbeatLoopAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> heartbeatInterval,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var delay = heartbeatInterval();
                if (delay <= TimeSpan.Zero)
                    delay = TimeSpan.FromSeconds(30);
                await Task.Delay(delay, ct).ConfigureAwait(false);
                await TryRenewRestoreLeaseAsync(scopeFactory, leaseTimeout, runId, logger, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static async Task TryRenewBackupLeaseAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.BackupRuns.FirstOrDefaultAsync(
                    r => r.Id == runId
                         && (r.Status == BackupRunStatus.Running || r.Status == BackupRunStatus.AwaitingVerification),
                    ct)
                .ConfigureAwait(false);
            if (run == null)
                return;
            var now = DateTime.UtcNow;
            var timeout = leaseTimeout();
            if (timeout <= TimeSpan.Zero)
                timeout = TimeSpan.FromMinutes(15);
            run.LastHeartbeatAtUtc = now;
            run.LeaseExpiresAtUtc = now + timeout;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Backup run lease heartbeat renew failed for runId={RunId}", runId);
        }
    }

    private static async Task TryRenewRestoreLeaseAsync(
        IServiceScopeFactory scopeFactory,
        Func<TimeSpan> leaseTimeout,
        Guid runId,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.RestoreVerificationRuns.FirstOrDefaultAsync(
                    r => r.Id == runId && r.Status == RestoreVerificationStatus.Running,
                    ct)
                .ConfigureAwait(false);
            if (run == null)
                return;
            var now = DateTime.UtcNow;
            var timeout = leaseTimeout();
            if (timeout <= TimeSpan.Zero)
                timeout = TimeSpan.FromMinutes(15);
            run.LastHeartbeatAtUtc = now;
            run.LeaseExpiresAtUtc = now + timeout;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Restore verification run lease heartbeat renew failed for runId={RunId}", runId);
        }
    }
}
