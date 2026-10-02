using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Hubs;

[Authorize]
public sealed class KitchenHub : Hub
{
    public const string HubPath = "/hubs/kitchen";
    public const string CreatedEvent = "KitchenOrderCreated";
    public const string StatusChangedEvent = "KitchenOrderStatusChanged";
    public const string ItemStatusChangedEvent = "KitchenOrderItemStatusChanged";

    private readonly IAuthorizationService _authorization;

    public KitchenHub(IAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public static string TenantGroup(Guid tenantId) => $"kitchen:{tenantId:D}";

    public async Task Subscribe()
    {
        var user = Context.User ?? throw new HubException("Forbidden");
        var view = await _authorization
            .AuthorizeAsync(user, PermissionCatalog.PolicyPrefix + AppPermissions.KitchenView)
            .ConfigureAwait(false);
        if (!view.Succeeded)
            throw new HubException("Forbidden");

        var tenantClaim = user.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
            throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId)).ConfigureAwait(false);
    }
}
