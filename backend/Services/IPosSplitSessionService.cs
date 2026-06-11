using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPosSplitSessionService
{
    Task<SplitSessionDto> StartSplitAsync(
        string cashierUserId,
        StartSplitRequest request,
        CancellationToken cancellationToken = default);

    Task AssignItemAsync(
        string cashierUserId,
        Guid sessionId,
        AssignItemRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> CompleteSplitAsync(
        string cashierUserId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SplitSessionDto?> GetSessionAsync(
        string cashierUserId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<SplitSessionDto> MergeSessionsToTableAsync(
        string cashierUserId,
        MergeSplitSessionsRequest request,
        CancellationToken cancellationToken = default);
}
