using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Feedback;

public interface IAdminFeedbackService
{
    Task<AdminFeedbackDto> CreateAsync(
        Guid tenantId,
        string userId,
        string? displayName,
        CreateAdminFeedbackRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AdminFeedbackListResponseDto> ListMineAsync(
        string userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<AdminFeedbackListResponseDto> ListAllAsync(
        string? status,
        string? category,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<AdminFeedbackDto?> UpdateStatusAsync(
        Guid id,
        string reviewerUserId,
        UpdateAdminFeedbackStatusRequestDto request,
        CancellationToken cancellationToken = default);
}
