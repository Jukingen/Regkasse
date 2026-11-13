using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.DigitalServices;

public interface ITenantServiceStatusService
{
    Task<IReadOnlyList<TenantDigitalServiceRowDto>> ListTenantStatusesAsync(CancellationToken ct = default);

    /// <summary>Single-tenant status for Manager portal / Super Admin tenant page. Null when tenant missing.</summary>
    Task<TenantDigitalServiceRowDto?> GetForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>True when Mandanten preference and Super Admin gate both allow the service.</summary>
    Task<bool> IsServiceAvailableAsync(Guid tenantId, string serviceType, CancellationToken ct = default);

    Task<TenantDigitalServiceMutationResponseDto> SetActiveAsync(
        Guid tenantId,
        string serviceType,
        bool active,
        string? actorUserId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>Mandanten preference: opt in/out of using the service (<c>IsEnabled</c>).</summary>
    Task<TenantDigitalServiceMutationResponseDto> SetEnabledAsync(
        Guid tenantId,
        string serviceType,
        bool enabled,
        string? actorUserId,
        CancellationToken ct = default);

    Task<TenantDigitalServiceMutationResponseDto> SetCustomPriceAsync(
        Guid tenantId,
        string serviceType,
        decimal? customPrice,
        string? actorUserId,
        CancellationToken ct = default);

    /// <summary>Mark service as pending creation request (Manager request).</summary>
    Task MarkRequestPendingAsync(Guid tenantId, string serviceType, CancellationToken ct = default);

    /// <summary>Clear pending request after Super Admin reject.</summary>
    Task MarkRequestRejectedAsync(Guid tenantId, string serviceType, CancellationToken ct = default);

    /// <summary>Clear pending after approve without generate (status returns to prior artifact state).</summary>
    Task ClearPendingRequestAsync(Guid tenantId, string serviceType, CancellationToken ct = default);

    /// <summary>Mark service as generated (Super Admin create).</summary>
    Task MarkCreatedAsync(
        Guid tenantId,
        string serviceType,
        string? url,
        string? templateId,
        CancellationToken ct = default);

    /// <summary>Mark service as published live.</summary>
    Task MarkPublishedAsync(
        Guid tenantId,
        string serviceType,
        string? url,
        CancellationToken ct = default);
}
