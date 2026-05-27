using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

/// <summary>TSE health, offline queue, and sync telemetry for cash register admin surfaces.</summary>
public sealed class CashRegisterHealthService : ICashRegisterHealthService
{
    private readonly AppDbContext _db;
    private readonly ITseHealthMonitor _health;
    private readonly IOptionsMonitor<TseOptions> _tseOptions;

    public CashRegisterHealthService(
        AppDbContext db,
        ITseHealthMonitor health,
        IOptionsMonitor<TseOptions> tseOptions)
    {
        _db = db;
        _health = health;
        _tseOptions = tseOptions;
    }

    public string MapTseHealthStatus(TseHealthSnapshot snapshot, bool tseConfigured)
    {
        if (!tseConfigured)
            return "notConfigured";

        if (!snapshot.HasCompletedProbe)
            return "notConfigured";

        return snapshot.Status switch
        {
            TseOperationalHealth.Online => "healthy",
            TseOperationalHealth.Degraded => "degraded",
            TseOperationalHealth.Offline => "offline",
            _ => "degraded",
        };
    }

    public async Task<CashRegisterTseHealthDto?> GetTseHealthForRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return null;

        var exists = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
            return null;

        var snapshot = _health.Snapshot;
        var configured = IsTseConfigured();
        var queueCount = await CountOfflineQueueAsync(cashRegisterId, cancellationToken).ConfigureAwait(false);

        return new CashRegisterTseHealthDto
        {
            Status = MapTseHealthStatus(snapshot, configured),
            LastCheckUtc = snapshot.LastCheckUtc,
            Message = snapshot.LastErrorMessageSafe,
            OfflineQueueCount = queueCount,
        };
    }

    public async Task ApplyOperationalFieldsAsync(
        IReadOnlyList<CashRegisterDto> dtos,
        IReadOnlyList<CashRegister> entities,
        CancellationToken cancellationToken = default)
    {
        if (dtos.Count == 0)
            return;

        var registerIds = dtos.Select(d => d.Id).Distinct().ToList();
        var entityById = entities.ToDictionary(e => e.Id);

        var offlineCounts = await LoadOfflineQueueCountsAsync(registerIds, cancellationToken).ConfigureAwait(false);
        var lastPaymentSync = await LoadLastPaymentSyncAsync(registerIds, cancellationToken).ConfigureAwait(false);
        var lastOfflineSync = await LoadLastOfflineFiscalizedAsync(registerIds, cancellationToken).ConfigureAwait(false);

        var tseSnapshot = _health.Snapshot;
        var tseConfigured = IsTseConfigured();
        var tseStatus = MapTseHealthStatus(tseSnapshot, tseConfigured);
        var globalHealthSync = tseSnapshot.LastSuccessfulPingUtc ?? tseSnapshot.LastCheckUtc;

        foreach (var dto in dtos)
        {
            if (entityById.TryGetValue(dto.Id, out var entity))
            {
                dto.LastMonatsbelegUtc = entity.LastMonatsbelegUtc;
                dto.LastJahresbelegUtc = entity.LastJahresbelegUtc;
                dto.CurrentCashierName = ResolveCashierDisplayName(entity);
            }

            dto.TseHealthStatus = tseStatus;
            dto.OfflineQueueCount = offlineCounts.GetValueOrDefault(dto.Id);

            var paymentSync = lastPaymentSync.GetValueOrDefault(dto.Id);
            var offlineSync = lastOfflineSync.GetValueOrDefault(dto.Id);
            dto.LastSyncAtUtc = MaxUtc(paymentSync, offlineSync, globalHealthSync);

            dto.DeviceInfo = new CashRegisterDeviceInfoDto();
        }
    }

    private bool IsTseConfigured() => !_tseOptions.CurrentValue.IsOff;

    private async Task<int> CountOfflineQueueAsync(Guid cashRegisterId, CancellationToken cancellationToken) =>
        await _db.OfflineTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(
                x => x.CashRegisterId == cashRegisterId
                     && (x.Status == OfflineTransactionStatus.Pending
                         || x.Status == OfflineTransactionStatus.NonFiscalPending),
                cancellationToken)
            .ConfigureAwait(false);

    private static string? ResolveCashierDisplayName(CashRegister register)
    {
        var user = register.CurrentUser;
        if (user == null)
            return null;

        if (!string.IsNullOrWhiteSpace(user.Name))
            return user.Name.Trim();

        if (!string.IsNullOrWhiteSpace(user.UserName))
            return user.UserName.Trim();

        return user.Id;
    }

    private async Task<Dictionary<Guid, int>> LoadOfflineQueueCountsAsync(
        IReadOnlyList<Guid> registerIds,
        CancellationToken cancellationToken) =>
        await _db.OfflineTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => registerIds.Contains(x.CashRegisterId)
                        && (x.Status == OfflineTransactionStatus.Pending
                            || x.Status == OfflineTransactionStatus.NonFiscalPending))
            .GroupBy(x => x.CashRegisterId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

    private async Task<Dictionary<Guid, DateTime>> LoadLastPaymentSyncAsync(
        IReadOnlyList<Guid> registerIds,
        CancellationToken cancellationToken) =>
        await _db.PaymentDetails
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => registerIds.Contains(p.CashRegisterId) && p.TseSignature != "")
            .GroupBy(p => p.CashRegisterId)
            .Select(g => new { g.Key, Max = g.Max(p => p.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Max, cancellationToken)
            .ConfigureAwait(false);

    private async Task<Dictionary<Guid, DateTime>> LoadLastOfflineFiscalizedAsync(
        IReadOnlyList<Guid> registerIds,
        CancellationToken cancellationToken) =>
        await _db.OfflineTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => registerIds.Contains(x.CashRegisterId) && x.FiscalizedAtUtc != null)
            .GroupBy(x => x.CashRegisterId)
            .Select(g => new { g.Key, Max = g.Max(x => x.FiscalizedAtUtc!.Value) })
            .ToDictionaryAsync(x => x.Key, x => x.Max, cancellationToken)
            .ConfigureAwait(false);

    private static DateTime? MaxUtc(params DateTime?[] values)
    {
        DateTime? max = null;
        foreach (var value in values)
        {
            if (!value.HasValue)
                continue;
            if (!max.HasValue || value.Value > max.Value)
                max = value;
        }

        return max;
    }
}
