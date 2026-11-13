using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Synchronous Foodora status write for one kitchen transition.
/// Logistics ready is <c>READY_FOR_PICKUP</c>. Vendor ready is <c>DISPATCHED</c>.
/// Neither transport sends a PUT when the local order becomes completed.
/// Does not change <see cref="OnlineOrder.OrderStatus"/> and does not schedule intake retries.
/// </summary>
public sealed class FoodoraOrderStatusSync
{
    public const string RejectedCode = "FOODORA_STATUS_REJECTED";
    public const string TokenCode = "FOODORA_TOKEN_UNAVAILABLE";

    private readonly IFoodoraAuthClient _foodora;
    private readonly IFoodoraAccessTokenSource _tokens;
    private readonly IVerticalProfileGuard _profiles;

    public FoodoraOrderStatusSync(
        IFoodoraAuthClient foodora,
        IFoodoraAccessTokenSource tokens,
        IVerticalProfileGuard profiles)
    {
        _foodora = foodora;
        _tokens = tokens;
        _profiles = profiles;
    }

    /// <summary>
    /// Null when this transition has no Foodora call and the local status may advance.
    /// A failed result leaves the online order unchanged.
    /// </summary>
    public async Task<OnlineOrderStatusUpdateResult?> ApplyAsync(
        AppDbContext db,
        OnlineOrder order,
        string nextStatus,
        CancellationToken ct)
    {
        var integration = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.OnlineOrderId == order.Id
                     && r.Provider == OnlineOrderIntegrationSignatureStatus.FoodoraProvider
                     && r.IsActive,
                ct);
        if (integration is null)
            return null;

        var action = ActionFor(integration.FoodoraTransportType, order.OrderStatus, nextStatus);
        if (action is null)
            return null;

        if (!await _profiles.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, ct))
        {
            return OnlineOrderStatusUpdateResult.Fail(
                ProfileEndpointDisabledException.Code,
                ProfileEndpointDisabledException.DefaultMessage);
        }

        var access = await _tokens.GetAccessAsync(order.TenantId, ct);
        if (access is null || string.IsNullOrWhiteSpace(access.AccessToken) || string.IsNullOrWhiteSpace(access.ChainId))
        {
            await RememberAsync(db, integration, "Foodora access token is not available.", ct);
            return OnlineOrderStatusUpdateResult.Fail(TokenCode, "Foodora access token is not available.");
        }

        var put = await _foodora.PutOrderStatusAsync(
            access.AccessToken,
            access.ChainId,
            integration.ExternalOrderId,
            action,
            ct);
        if (put.Succeeded || await AlreadyInTargetStateAsync(access, integration.ExternalOrderId, action, put.StatusCode, ct))
        {
            integration.LastError = null;
            return null;
        }

        var message = put.StatusCode is StatusCodes.Status409Conflict or StatusCodes.Status422UnprocessableEntity
            ? $"Foodora rejected {action} with HTTP {put.StatusCode}."
            : $"Foodora {action} failed with HTTP {put.StatusCode}.";
        await RememberAsync(db, integration, message, ct);
        return OnlineOrderStatusUpdateResult.Fail(RejectedCode, message);
    }

    internal static string? ActionFor(string? transportType, string current, string next)
    {
        var from = current.Trim().ToLowerInvariant();
        var to = next.Trim().ToLowerInvariant();
        if (to == OnlineOrderStatuses.Cancelled && from != OnlineOrderStatuses.Cancelled)
            return "CANCELLED";
        if (from == OnlineOrderStatuses.Preparing && to == OnlineOrderStatuses.Ready)
        {
            return string.Equals(transportType?.Trim(), FoodoraOrderMapper.VendorDelivery, StringComparison.OrdinalIgnoreCase)
                ? "DISPATCHED"
                : "READY_FOR_PICKUP";
        }

        return null;
    }

    private async Task<bool> AlreadyInTargetStateAsync(
        FoodoraAccess access,
        string orderId,
        string action,
        int statusCode,
        CancellationToken ct)
    {
        if (statusCode != StatusCodes.Status409Conflict)
            return false;

        var fetched = await _foodora.GetOrderAsync(access.AccessToken, access.ChainId, orderId, ct);
        if (!fetched.Succeeded)
            return false;

        var current = FoodoraOrderMapper.ReadStatus(fetched.Body);
        return string.Equals(current, action, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RememberAsync(
        AppDbContext db,
        OnlineOrderIntegration integration,
        string message,
        CancellationToken ct)
    {
        integration.LastError = message.Length <= 500 ? message : message[..500];
        integration.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
