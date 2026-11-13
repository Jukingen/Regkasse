using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KasseAPI_Final.Hubs;

[Authorize]
public sealed class FiskalyOperationStatusHub : Hub
{
    public const string ClientEventName = "OperationStatus";
    public const string BatchClientEventName = "BatchProgress";
    public const string SuperAdminGroupName = "fiskaly-ops:all";
    public const string HubPath = "/hubs/fiskaly-operation-status";

    private readonly IAuthorizationService _authorization;

    public FiskalyOperationStatusHub(IAuthorizationService authorization)
    {
        _authorization = authorization;
    }

    public static string TenantGroup(Guid tenantId) => $"fiskaly-ops:{tenantId:D}";

    public async Task Subscribe()
    {
        var user = Context.User ?? throw new HubException("Forbidden");
        var history = await _authorization
            .AuthorizeAsync(user, PermissionCatalog.PolicyPrefix + AppPermissions.FiskalyHistoryView)
            .ConfigureAwait(false);
        var operations = await _authorization
            .AuthorizeAsync(user, PermissionCatalog.PolicyPrefix + AppPermissions.FiskalyOperationsView)
            .ConfigureAwait(false);
        var depExport = await _authorization
            .AuthorizeAsync(user, PermissionCatalog.PolicyPrefix + AppPermissions.ReportExport)
            .ConfigureAwait(false);
        if (!history.Succeeded && !operations.Succeeded && !depExport.Succeeded)
            throw new HubException("Forbidden");

        var tenantClaim = user.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId) && tenantId != Guid.Empty)
            await Groups.AddToGroupAsync(Context.ConnectionId, TenantGroup(tenantId)).ConfigureAwait(false);

        if (user.IsInRole(Roles.SuperAdmin)
            || user.HasPermissionClaim(AppPermissions.SystemCritical))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, SuperAdminGroupName).ConfigureAwait(false);
        }
    }
}
