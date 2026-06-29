using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Offline;

public interface IOfflineOrderService
{
    Task<OfflineOrderResponse> SaveOfflineOrderAsync(
        OfflineOrderRequest request,
        CancellationToken ct = default);

    Task<List<OfflineOrderResponse>> GetPendingOrdersAsync(
        Guid cashRegisterId,
        CancellationToken ct = default);

    Task<ReplayOfflineOrdersResult> ReplayPendingOrdersAsync(
        Guid cashRegisterId,
        CancellationToken ct = default);

    Task<int> CleanupExpiredOrdersAsync(
        CancellationToken ct = default);

    Task<OfflineOrderResponse> GetOrderStatusAsync(
        string offlineOrderId,
        CancellationToken ct = default);

    Task<AdminOfflineOrdersListResponse> ListOrdersForAdminAsync(
        AdminOfflineOrdersListQuery query,
        CancellationToken ct = default);

    Task<ReplayOfflineOrderResult> ReplayOrderByIdAsync(
        Guid orderId,
        CancellationToken ct = default);

    Task<ReplayOfflineOrdersResult> ReplayAllPendingForTenantAsync(
        Guid? cashRegisterId = null,
        CancellationToken ct = default);
}
