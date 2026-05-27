using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.RestoreVerification;

public interface IManualRestoreTriggerService
{
    Task<RestoreRequestStatus> CreateRequestAsync(
        RestoreRequest request,
        string actorUserId,
        string? actorEmail,
        string? correlationId,
        CancellationToken cancellationToken = default);

    Task<RestoreRequestStatus> ProcessApprovalAsync(
        Guid requestId,
        RestoreApprovalRequest request,
        string actorUserId,
        string? actorEmail,
        string? correlationId,
        CancellationToken cancellationToken = default);

    Task<RestoreRequestStatus?> GetStatusAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<RestoreRequestHistoryResponse> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
