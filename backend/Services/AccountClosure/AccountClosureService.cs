using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataDeletion;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AccountClosure;

/// <summary>
/// Account closure / GDPR delete for Archived tenants.
/// Reuses <see cref="IDataDeletionService"/> (7-day wait after confirm; RKSV rows retained).
/// </summary>
public sealed class AccountClosureService : IAccountClosureService
{
    private readonly IDataDeletionService _deletion;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AccountClosureService> _logger;

    public AccountClosureService(
        IDataDeletionService deletion,
        IDbContextFactory<AppDbContext> dbFactory,
        ILogger<AccountClosureService> logger)
    {
        _deletion = deletion;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<ClosureResult> RequestClosureAsync(
        Guid tenantId,
        string? reason,
        string? requestedByUserId = null,
        CancellationToken ct = default)
    {
        try
        {
            var hasRksv = await HasRksvDataAsync(tenantId, ct).ConfigureAwait(false);
            var deletion = await _deletion
                .RequestDeletionAsync(tenantId, requestedByUserId, reason, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Account closure requested. TenantId={TenantId}, ClosureId={ClosureId}, HasRksvData={HasRksv}",
                tenantId,
                deletion.Id,
                hasRksv);

            return ClosureResult.FromDeletion(deletion, tenantId, hasRksv);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.NotFound);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Archived", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.NotArchived);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already purged", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.AlreadyPurged);
        }
        catch (InvalidOperationException ex)
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.InvalidStatus);
        }
    }

    public async Task<ClosureResult> GetClosureStatusAsync(Guid tenantId, CancellationToken ct = default)
    {
        var open = await _deletion.GetLatestOpenDeletionAsync(tenantId, ct).ConfigureAwait(false);
        if (open == null)
        {
            // Fall back to latest completed/cancelled for visibility.
            await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var latest = await db.TenantDataDeletionRequests.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.RequestedAtUtc)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (latest == null)
                return ClosureResult.Fail("No account closure request found", DataDeletionErrorCodes.NotFound);

            var hasRksv = await HasRksvDataAsync(tenantId, ct).ConfigureAwait(false);
            return ClosureResult.FromDeletion(DataDeletionService.Map(latest), tenantId, hasRksv);
        }

        return ClosureResult.FromDeletion(
            open,
            tenantId,
            await HasRksvDataAsync(tenantId, ct).ConfigureAwait(false));
    }

    public async Task<ClosureResult> CancelClosureAsync(
        Guid tenantId,
        string? cancelledByUserId = null,
        CancellationToken ct = default)
    {
        var open = await _deletion.GetLatestOpenDeletionAsync(tenantId, ct).ConfigureAwait(false);
        if (open == null)
            return ClosureResult.Fail("No open account closure request to cancel", DataDeletionErrorCodes.NotFound);

        try
        {
            var cancelled = await _deletion
                .CancelDeletionAsync(tenantId, open.Id, cancelledByUserId, ct)
                .ConfigureAwait(false);

            return ClosureResult.FromDeletion(
                cancelled,
                tenantId,
                await HasRksvDataAsync(tenantId, ct).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.NotFound);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already completed", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.AlreadyCompleted);
        }
        catch (InvalidOperationException ex)
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.InvalidStatus);
        }
    }

    public async Task<ClosureResult> ConfirmClosureAsync(
        Guid tenantId,
        Guid closureId,
        string? confirmedByUserId = null,
        CancellationToken ct = default)
    {
        try
        {
            var confirmed = await _deletion
                .ConfirmDeletionAsync(tenantId, closureId, confirmedByUserId, ct)
                .ConfigureAwait(false);

            return ClosureResult.FromDeletion(
                confirmed,
                tenantId,
                await HasRksvDataAsync(tenantId, ct).ConfigureAwait(false));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return ClosureResult.Fail(ex.Message, DataDeletionErrorCodes.InvalidStatus);
        }
    }

    public async Task<ClosureResult> ExecuteClosureAsync(
        Guid closureId,
        string? actorUserId = null,
        string executedVia = TenantDataDeletionExecutedVia.Manual,
        CancellationToken ct = default)
    {
        var purge = await _deletion
            .ExecutePurgeAsync(closureId, actorUserId, executedVia, ct)
            .ConfigureAwait(false);

        if (!purge.Succeeded)
        {
            return new ClosureResult
            {
                Succeeded = false,
                Error = purge.Error,
                ErrorCode = purge.ErrorCode,
                ClosureId = purge.RequestId ?? closureId,
                TenantId = purge.TenantId,
            };
        }

        var tenantId = purge.TenantId ?? Guid.Empty;
        var closureStatus = await GetClosureStatusAsync(tenantId, ct).ConfigureAwait(false);

        return new ClosureResult
        {
            Succeeded = true,
            ClosureId = purge.RequestId,
            TenantId = purge.TenantId,
            Status = TenantDataDeletionRequestStatuses.Completed,
            CompletedAtUtc = closureStatus.CompletedAtUtc ?? DateTime.UtcNow,
            HasRksvData = true, // RKSV retained by design
            ConfirmationWaitDays = DataDeletionService.ConfirmationWaitDays,
            DeletedCounts = purge.DeletedCounts,
            RequestedAtUtc = closureStatus.RequestedAtUtc,
            ConfirmedAtUtc = closureStatus.ConfirmedAtUtc,
            ScheduledPurgeAtUtc = closureStatus.ScheduledPurgeAtUtc,
        };
    }

    /// <summary>
    /// Fiscal payments are scoped via cash registers (not a direct tenant_id on payment_details).
    /// </summary>
    private async Task<bool> HasRksvDataAsync(Guid tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var hasReceipts = await db.Receipts.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(r => r.TenantId == tenantId, ct)
            .ConfigureAwait(false);
        if (hasReceipts)
            return true;

        var cashRegisterIds = await db.CashRegisters.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (cashRegisterIds.Count == 0)
            return false;

        return await db.PaymentDetails.AsNoTracking()
            .AnyAsync(p => cashRegisterIds.Contains(p.CashRegisterId), ct)
            .ConfigureAwait(false);
    }
}
