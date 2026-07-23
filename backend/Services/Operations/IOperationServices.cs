using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Operations;

public interface IOperationLogService
{
    Task<OperationLog> LogAsync(
        Guid tenantId,
        string userId,
        string operationType,
        string entityType,
        string entityId,
        object? beforeState,
        object? afterState,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<OperationLogListResponseDto> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? operationType = null,
        bool? isUndone = null,
        CancellationToken cancellationToken = default);

    Task<OperationLogDetailDto?> GetAsync(
        Guid tenantId,
        Guid operationId,
        CancellationToken cancellationToken = default);

    bool IsUndoable(string operationType);
}

public interface IOperationUndoService
{
    Task<UndoOperationResponse> UndoOperationAsync(
        Guid tenantId,
        Guid operationId,
        string undoByUserId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}

public readonly record struct UndoResult(bool Ok, string? ErrorCode, string? Message)
{
    public static UndoResult Success() => new(true, null, null);
    public static UndoResult Fail(string code, string message) => new(false, code, message);
}
