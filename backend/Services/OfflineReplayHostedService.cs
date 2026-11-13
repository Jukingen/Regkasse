using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>
/// Replays server-queued <see cref="OfflineTransactionStatus.NonFiscalPending"/> intents when TSE health is Online.
/// </summary>
public sealed class OfflineReplayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITseHealthMonitor _health;
    private readonly ILogger<OfflineReplayHostedService> _logger;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;

    public OfflineReplayHostedService(
        IServiceScopeFactory scopeFactory,
        ITseHealthMonitor health,
        ILogger<OfflineReplayHostedService> logger,
        IOptionsMonitor<TseOptions> tseOptions)
    {
        _scopeFactory = scopeFactory;
        _health = health;
        _logger = logger;
        _tseOptions = tseOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _tseOptions.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Clamp(opts.AutoReplayIntervalSeconds, 10, 3600));

            if (!opts.OfflineModeEnabled || opts.IsOff || opts.UseSoftTseWhenNoDevice)
            {
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

            if (_health.Snapshot.Status != TseOperationalHealth.Online)
            {
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
                await ReplayPendingOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Automatic offline replay cycle failed");
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

    /// <summary>
    /// One replay pass. Tenant filters are fail-closed, so each active tenant gets its own scope
    /// before the pending queue is read.
    /// </summary>
    public async Task ReplayPendingOnceAsync(CancellationToken cancellationToken)
    {
        List<Guid> tenantIds;
        await using (var listScope = _scopeFactory.CreateAsyncScope())
        {
            var listDb = listScope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantIds = await listDb.Tenants.AsNoTracking()
                .Where(t => t.IsActive)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var tenantId in tenantIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = _scopeFactory.CreateAsyncScope();
            var accessor = scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>();
            accessor.TenantId = tenantId;
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var offlineService = scope.ServiceProvider.GetRequiredService<IOfflineTransactionService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOfflineReplayCompletionNotifier>();

            var pending = await db.OfflineTransactions.AsNoTracking()
                .Where(x => x.Status == OfflineTransactionStatus.NonFiscalPending)
                .OrderBy(x => x.ServerReceivedAtUtc)
                .Take(100)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (pending.Count == 0)
                continue;

            var distinctRegisters = pending.Select(x => x.CashRegisterId).Distinct().ToList();
            foreach (var registerId in distinctRegisters)
            {
                var batchForRegister = pending.Where(x => x.CashRegisterId == registerId).ToList();
                var transactions = new List<ReplayOfflineTransactionItem>(batchForRegister.Count);
                foreach (var row in batchForRegister)
                {
                    var payloadElement = JsonSerializer.Deserialize<JsonElement>(row.PayloadJson);
                    transactions.Add(new ReplayOfflineTransactionItem
                    {
                        OfflineTransactionId = row.Id,
                        CashRegisterId = row.CashRegisterId,
                        CreatedAtUtc = row.OfflineCreatedAtUtc,
                        Payload = payloadElement,
                        DeviceId = row.DeviceId,
                        ClientSequenceNumber = row.ClientSequenceNumber
                    });
                }

                var userId = batchForRegister[0].CreatedBy ?? "system-offline-replay";
                var replayReq = new ReplayOfflineTransactionsRequest { Transactions = transactions };

                _logger.LogInformation(
                    "Starting automatic offline replay batch for TenantId={TenantId} CashRegisterId={CashRegisterId} ItemCount={Count}",
                    tenantId,
                    registerId,
                    transactions.Count);

                await offlineService.ReplayOfflineTransactionsAsync(replayReq, userId, "Cashier")
                    .ConfigureAwait(false);

                await notifier.NotifyReplayBatchCompletedAsync(registerId, transactions.Count, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
