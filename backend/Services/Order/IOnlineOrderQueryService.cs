using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderQueryService
{
    Task<OnlineOrderListResponseDto> ListAsync(
        string? status = null,
        int take = 100,
        CancellationToken ct = default);

    Task<OnlineOrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<OnlineOrderAnalyticsDto> GetAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default);

    /// <summary>
    /// Public lookup by tenant slug + order number (optional phone match).
    /// Cross-tenant safe — does not use ambient tenant filter.
    /// </summary>
    Task<PublicOnlineOrderStatusDto?> GetPublicStatusAsync(
        string tenantSlug,
        string orderNumber,
        string? customerPhone = null,
        CancellationToken ct = default);
}
