using KasseAPI_Final.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Services.Kitchen;

public interface IKitchenOrderBroadcaster
{
    Task OrderCreatedAsync(KitchenOrderDto order, CancellationToken cancellationToken);
    Task OrderStatusChangedAsync(KitchenOrderDto order, CancellationToken cancellationToken);
    Task ItemStatusChangedAsync(KitchenOrderDto order, Guid itemId, CancellationToken cancellationToken);
}

public sealed class KitchenOrderBroadcaster : IKitchenOrderBroadcaster
{
    private readonly IHubContext<KitchenHub> _hub;
    private readonly ILogger<KitchenOrderBroadcaster> _logger;

    public KitchenOrderBroadcaster(IHubContext<KitchenHub> hub, ILogger<KitchenOrderBroadcaster> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task OrderCreatedAsync(KitchenOrderDto order, CancellationToken cancellationToken) =>
        SendAsync(KitchenHub.CreatedEvent, order, cancellationToken);

    public Task OrderStatusChangedAsync(KitchenOrderDto order, CancellationToken cancellationToken) =>
        SendAsync(KitchenHub.StatusChangedEvent, order, cancellationToken);

    public Task ItemStatusChangedAsync(KitchenOrderDto order, Guid itemId, CancellationToken cancellationToken) =>
        SendAsync(KitchenHub.ItemStatusChangedEvent, new { order, itemId }, cancellationToken, order.TenantId);

    private async Task SendAsync(string eventName, object payload, CancellationToken cancellationToken, Guid? tenantId = null)
    {
        var groupTenant = tenantId
            ?? (payload as KitchenOrderDto)?.TenantId
            ?? Guid.Empty;
        if (groupTenant == Guid.Empty)
            return;

        try
        {
            await _hub.Clients
                .Group(KitchenHub.TenantGroup(groupTenant))
                .SendAsync(eventName, payload, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kitchen hub broadcast failed for {Event}", eventName);
        }
    }
}
