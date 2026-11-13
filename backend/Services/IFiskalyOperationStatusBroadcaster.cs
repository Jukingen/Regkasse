using KasseAPI_Final.DTOs;
using KasseAPI_Final.Hubs;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Services;

public interface IFiskalyOperationStatusBroadcaster
{
    Task PublishAsync(FiskalyOperationStatusEventDto evt, CancellationToken cancellationToken = default);

    Task PublishBatchProgressAsync(FiskalyBatchProgressEventDto evt, CancellationToken cancellationToken = default);
}

public static class FiskalyOperationStatusMapper
{
    public static FiskalyOperationStatusEventDto FromRow(FiskalyOperationHistory row) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            OperationType = row.OperationType,
            Status = row.Status,
            ProgressPercent = FiskalyOperationHistoryStatuses.ProgressPercent(row.Status),
            CashRegisterId = row.CashRegisterId,
            CashRegisterName = row.CashRegisterName,
            ReceiptNumber = row.ReceiptNumber,
            ErrorCode = row.ErrorCode,
            ErrorMessage = row.ErrorMessage,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc
        };
}

public sealed class FiskalyOperationStatusBroadcaster : IFiskalyOperationStatusBroadcaster
{
    private readonly IHubContext<FiskalyOperationStatusHub> _hub;
    private readonly ILogger<FiskalyOperationStatusBroadcaster> _logger;

    public FiskalyOperationStatusBroadcaster(
        IHubContext<FiskalyOperationStatusHub> hub,
        ILogger<FiskalyOperationStatusBroadcaster> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(FiskalyOperationStatusEventDto evt, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients
                .Group(FiskalyOperationStatusHub.TenantGroup(evt.TenantId))
                .SendAsync(FiskalyOperationStatusHub.ClientEventName, evt, cancellationToken)
                .ConfigureAwait(false);

            await _hub.Clients
                .Group(FiskalyOperationStatusHub.SuperAdminGroupName)
                .SendAsync(FiskalyOperationStatusHub.ClientEventName, evt, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast Fiskaly operation status for {HistoryId}", evt.Id);
        }
    }

    public async Task PublishBatchProgressAsync(
        FiskalyBatchProgressEventDto evt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (evt.TenantId is { } tenantId && tenantId != Guid.Empty)
            {
                await _hub.Clients
                    .Group(FiskalyOperationStatusHub.TenantGroup(tenantId))
                    .SendAsync(FiskalyOperationStatusHub.BatchClientEventName, evt, cancellationToken)
                    .ConfigureAwait(false);
            }

            await _hub.Clients
                .Group(FiskalyOperationStatusHub.SuperAdminGroupName)
                .SendAsync(FiskalyOperationStatusHub.BatchClientEventName, evt, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast Fiskaly batch progress for {BatchId}", evt.BatchId);
        }
    }
}
