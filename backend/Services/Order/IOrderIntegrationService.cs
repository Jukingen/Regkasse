using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>Pushes an <see cref="OnlineOrder"/> into the live POS cart surface (no fictional PosOrder entity).</summary>
public interface IOrderIntegrationService
{
    /// <summary>
    /// Materializes <paramref name="order"/> as an active <see cref="Cart"/> for <paramref name="claimingUserId"/>,
    /// links <see cref="OnlineOrder.PosCartId"/>, and publishes an FA activity notification.
    /// Idempotent when already pushed.
    /// </summary>
    Task<OrderIntegrationResult> PushOrderToPosAsync(
        OnlineOrder order,
        string claimingUserId,
        CancellationToken ct = default);

    /// <summary>Loads the online order by id (tenant-filtered) then pushes.</summary>
    Task<OrderIntegrationResult> PushOrderToPosAsync(
        Guid onlineOrderId,
        string claimingUserId,
        CancellationToken ct = default);
}
