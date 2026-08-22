using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Activity;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Counts live catalog/membership/sale volume against <see cref="TenantLimits"/>.</summary>
public sealed class TenantLimitGuard : ITenantLimitGuard
{
    private readonly AppDbContext _db;
    private readonly ITenantLimitService _limits;
    private readonly ILogger<TenantLimitGuard> _logger;
    private readonly IActivityEventPublisher? _activity;

    public TenantLimitGuard(
        AppDbContext db,
        ITenantLimitService limits,
        ILogger<TenantLimitGuard> logger,
        IActivityEventPublisher? activity = null)
    {
        _db = db;
        _limits = limits;
        _logger = logger;
        _activity = activity;
    }

    public async Task EnsureCanCreateProductAsync(
        Guid tenantId,
        int additionalCount = 1,
        CancellationToken cancellationToken = default)
    {
        if (additionalCount <= 0)
            return;

        var max = await _limits
            .GetLimitValueAsync(tenantId, TenantLimitKeys.MaxProductsPerTenant, cancellationToken)
            .ConfigureAwait(false);
        var current = await CountActiveProductsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (current + additionalCount > max)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxProductsPerTenant,
                        max,
                        current,
                        $"Maximum {max} products per tenant reached"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var max = await _limits
            .GetLimitValueAsync(tenantId, TenantLimitKeys.MaxUsersPerTenant, cancellationToken)
            .ConfigureAwait(false);
        var current = await CountActiveUsersAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (current >= max)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxUsersPerTenant,
                        max,
                        current,
                        $"Maximum {max} users per tenant reached"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task EnsureSaleWithinLimitsAsync(
        Guid tenantId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var caps = await _limits.GetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var (dayStart, dayEnd) = UtcDayBounds();

        var todayCount = await TodaysPayments(tenantId, dayStart, dayEnd)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        if (todayCount >= caps.DailyMaxTransactions)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.DailyMaxTransactions,
                        caps.DailyMaxTransactions,
                        todayCount,
                        $"Daily transaction limit of {caps.DailyMaxTransactions} reached"),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (amount > caps.MaxTransactionAmount)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxTransactionAmount,
                        caps.MaxTransactionAmount,
                        amount,
                        $"Maximum transaction amount is {caps.MaxTransactionAmount:0.##}"),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var todayRevenue = await TodaysPayments(tenantId, dayStart, dayEnd)
            .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;
        if (todayRevenue + amount > caps.DailyMaxRevenue)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.DailyMaxRevenue,
                        caps.DailyMaxRevenue,
                        todayRevenue,
                        $"Daily revenue limit of {caps.DailyMaxRevenue:0.##} would be exceeded"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task EnsureCanCreateBackupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var caps = await _limits.GetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var currentBackups = await _db.BackupRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(
                r => r.TenantId == tenantId
                     && r.Strategy == BackupStrategyKind.Tenant
                     && r.Status == BackupRunStatus.Succeeded,
                cancellationToken)
            .ConfigureAwait(false);
        if (currentBackups >= caps.MaxBackupsPerTenant)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxBackupsPerTenant,
                        caps.MaxBackupsPerTenant,
                        currentBackups,
                        $"Maximum {caps.MaxBackupsPerTenant} backups per tenant reached"),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var maxSizeBytes = (long)caps.MaxBackupSizeMb * 1024L * 1024L;
        var totalSize = await SucceededTenantLogicalDumps(tenantId)
            .SumAsync(a => a.ByteSize ?? 0L, cancellationToken)
            .ConfigureAwait(false);
        var lastBackupSize = await (
                from a in _db.BackupArtifacts.IgnoreQueryFilters().AsNoTracking()
                join r in _db.BackupRuns.IgnoreQueryFilters().AsNoTracking() on a.BackupRunId equals r.Id
                where r.TenantId == tenantId
                      && r.Strategy == BackupStrategyKind.Tenant
                      && r.Status == BackupRunStatus.Succeeded
                      && a.ArtifactType == BackupArtifactType.LogicalDump
                orderby r.RequestedAt descending
                select a.ByteSize ?? 0L)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (totalSize >= maxSizeBytes || totalSize + lastBackupSize > maxSizeBytes)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxBackupSizeMb,
                        caps.MaxBackupSizeMb,
                        totalSize / (1024m * 1024m),
                        $"Total backup size would exceed {caps.MaxBackupSizeMb}MB limit"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task EnsureCanQueueOfflineTransactionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var max = await _limits
            .GetLimitValueAsync(tenantId, TenantLimitKeys.MaxOfflineTransactions, cancellationToken)
            .ConfigureAwait(false);
        var currentQueue = await CountQueuedOfflineTransactionsAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (currentQueue >= max)
        {
            await ThrowExceededAsync(
                    tenantId,
                    new LimitExceededException(
                        TenantLimitKeys.MaxOfflineTransactions,
                        max,
                        currentQueue,
                        $"Offline queue limit of {max} reached"),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<TenantLimitUsageDto> GetUsageAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var caps = await _limits.GetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var (dayStart, dayEnd) = UtcDayBounds();

        var currentProducts = await CountActiveProductsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currentUsers = await CountActiveUsersAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var currentDailyTransactions = await TodaysPayments(tenantId, dayStart, dayEnd)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentDailyRevenue = await TodaysPayments(tenantId, dayStart, dayEnd)
            .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var currentBackups = await _db.BackupRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(
                r => r.TenantId == tenantId
                     && r.Strategy == BackupStrategyKind.Tenant
                     && r.Status == BackupRunStatus.Succeeded,
                cancellationToken)
            .ConfigureAwait(false);
        var currentBackupBytes = await SucceededTenantLogicalDumps(tenantId)
            .SumAsync(a => a.ByteSize ?? 0L, cancellationToken)
            .ConfigureAwait(false);
        var currentOffline = await CountQueuedOfflineTransactionsAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        var assignmentCounts = await _db.CashRegisters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                        && r.AssignedUserId != null
                        && r.Status != RegisterStatus.Decommissioned)
            .GroupBy(r => r.AssignedUserId)
            .Select(g => g.Count())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentMaxAssignedRegistersPerUser = assignmentCounts.Count == 0 ? 0 : assignmentCounts.Max();

        _logger.LogDebug(
            "Tenant limit usage TenantId={TenantId} products={Products}/{MaxProducts} users={Users}/{MaxUsers} tx={Tx}/{MaxTx}",
            tenantId,
            currentProducts,
            caps.MaxProductsPerTenant,
            currentUsers,
            caps.MaxUsersPerTenant,
            currentDailyTransactions,
            caps.DailyMaxTransactions);

        return new TenantLimitUsageDto
        {
            TenantId = tenantId,
            Limits = TenantLimitsDto.FromEntity(caps),
            CurrentProducts = currentProducts,
            CurrentUsers = currentUsers,
            CurrentDailyTransactions = currentDailyTransactions,
            CurrentDailyRevenue = currentDailyRevenue,
            CurrentBackups = currentBackups,
            CurrentBackupSizeMb = currentBackupBytes / (1024m * 1024m),
            CurrentOfflineTransactions = currentOffline,
            CurrentMaxAssignedRegistersPerUser = currentMaxAssignedRegistersPerUser,
        };
    }

    private Task<int> CountActiveProductsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId && p.IsActive, cancellationToken);

    private Task<int> CountActiveUsersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, cancellationToken);

    private IQueryable<PaymentDetails> TodaysPayments(Guid tenantId, DateTime dayStart, DateTime dayEnd) =>
        from p in _db.PaymentDetails.IgnoreQueryFilters().AsNoTracking()
        join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on p.CashRegisterId equals r.Id
        where r.TenantId == tenantId && p.CreatedAt >= dayStart && p.CreatedAt < dayEnd
        select p;

    private IQueryable<BackupArtifact> SucceededTenantLogicalDumps(Guid tenantId) =>
        from a in _db.BackupArtifacts.IgnoreQueryFilters().AsNoTracking()
        join r in _db.BackupRuns.IgnoreQueryFilters().AsNoTracking() on a.BackupRunId equals r.Id
        where r.TenantId == tenantId
              && r.Strategy == BackupStrategyKind.Tenant
              && r.Status == BackupRunStatus.Succeeded
              && a.ArtifactType == BackupArtifactType.LogicalDump
        select a;

    private Task<int> CountQueuedOfflineTransactionsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        (
            from o in _db.OfflineTransactions.IgnoreQueryFilters().AsNoTracking()
            join r in _db.CashRegisters.IgnoreQueryFilters().AsNoTracking() on o.CashRegisterId equals r.Id
            where r.TenantId == tenantId
                  && (o.Status == OfflineTransactionStatus.Pending
                      || o.Status == OfflineTransactionStatus.NonFiscalPending)
            select o.Id
        ).CountAsync(cancellationToken);

    private static (DateTime Start, DateTime End) UtcDayBounds()
    {
        var start = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        return (start, start.AddDays(1));
    }

    private async Task ThrowExceededAsync(
        Guid tenantId,
        LimitExceededException exception,
        CancellationToken cancellationToken)
    {
        if (_activity != null)
        {
            await _activity
                .TryPublishAsync(
                    LimitDashboardMapper.ToPublishRequest(
                        tenantId,
                        ActivityEventType.LimitExceeded,
                        exception.LimitKey,
                        exception.LimitAmount,
                        exception.CurrentAmount),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw exception;
    }
}
