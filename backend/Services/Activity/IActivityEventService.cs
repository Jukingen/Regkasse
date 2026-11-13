using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services.Activity;

public interface IActivityEventService
{
    Task<ActivityEvent> PublishAsync(ActivityEventPublishRequest request, CancellationToken cancellationToken = default);

    Task<ActivitiesListResponseDto> ListAsync(
        string userId,
        Guid tenantId,
        int limit,
        int offset,
        string? severityFilter,
        CancellationToken cancellationToken = default,
        bool restrictToTenantStaff = false);

    Task<ActivitiesUnreadCountDto> GetUnreadCountAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default,
        bool restrictToTenantStaff = false);

    Task<ActivityDto?> MarkEventReadAsync(
        string userId,
        Guid tenantId,
        Guid activityId,
        CancellationToken cancellationToken = default,
        bool restrictToTenantStaff = false);

    Task<int> MarkAllReadAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default,
        bool restrictToTenantStaff = false);

    Task<bool> DeleteAsync(
        Guid tenantId,
        Guid activityId,
        CancellationToken cancellationToken = default,
        bool restrictToTenantStaff = false);

    /// <summary>Removes events older than configured retention (all tenants).</summary>
    Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}
