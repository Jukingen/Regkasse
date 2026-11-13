using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Synchronous Wolt status write for one kitchen transition.
/// Does not change <see cref="OnlineOrder.OrderStatus"/> and does not schedule intake retries.
/// </summary>
public sealed class WoltOrderStatusSync
{
    public const string RejectedCode = "WOLT_STATUS_REJECTED";
    public const string TokenCode = "WOLT_TOKEN_UNAVAILABLE";

    private readonly IWoltOAuthClient _wolt;
    private readonly IWoltAccessTokenSource _tokens;
    private readonly IVerticalProfileGuard _profiles;

    public WoltOrderStatusSync(
        IWoltOAuthClient wolt,
        IWoltAccessTokenSource tokens,
        IVerticalProfileGuard profiles)
    {
        _wolt = wolt;
        _tokens = tokens;
        _profiles = profiles;
    }

    /// <summary>
    /// Null when this transition has no Wolt call and the local status may advance.
    /// A failed result leaves the online order unchanged.
    /// </summary>
    public async Task<OnlineOrderStatusUpdateResult?> ApplyAsync(
        AppDbContext db,
        OnlineOrder order,
        string nextStatus,
        CancellationToken ct)
    {
        var action = ActionFor(order.OrderStatus, nextStatus);
        var integration = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                r => r.OnlineOrderId == order.Id
                     && r.Provider == OnlineOrderIntegrationSignatureStatus.WoltProvider
                     && r.IsActive,
                ct);
        if (integration is null || action is null)
            return null;

        if (action == "delivered")
        {
            var selfDelivery = await ResolveSelfDeliveryAsync(db, integration, ct);
            if (selfDelivery.Blocked is not null)
                return selfDelivery.Blocked;
            if (selfDelivery.Value != true)
                return null;
        }

        if (!await _profiles.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, ct))
        {
            return OnlineOrderStatusUpdateResult.Fail(
                ProfileEndpointDisabledException.Code,
                ProfileEndpointDisabledException.DefaultMessage);
        }

        var venueId = integration.ExternalLocationId?.Trim() ?? string.Empty;
        var access = venueId.Length == 0
            ? null
            : await _tokens.GetAccessTokenAsync(order.TenantId, venueId, ct);
        if (string.IsNullOrWhiteSpace(access))
        {
            await RememberAsync(db, integration, "Wolt access token is not available.", ct);
            return OnlineOrderStatusUpdateResult.Fail(TokenCode, "Wolt access token is not available.");
        }

        var put = await _wolt.PutOrderActionAsync(access, integration.ExternalOrderId, action, ct);
        if (put.Succeeded || await AlreadyInTargetStateAsync(access, integration.ExternalOrderId, action, put.StatusCode, ct))
        {
            integration.LastError = null;
            return null;
        }

        var message = put.StatusCode is StatusCodes.Status409Conflict or StatusCodes.Status422UnprocessableEntity
            ? $"Wolt rejected {action} with HTTP {put.StatusCode}."
            : $"Wolt {action} failed with HTTP {put.StatusCode}.";
        await RememberAsync(db, integration, message, ct);
        return OnlineOrderStatusUpdateResult.Fail(RejectedCode, message);
    }

    private async Task<(bool? Value, OnlineOrderStatusUpdateResult? Blocked)> ResolveSelfDeliveryAsync(
        AppDbContext db,
        OnlineOrderIntegration integration,
        CancellationToken ct)
    {
        if (integration.WoltSelfDelivery is bool known)
            return (known, null);

        if (!await _profiles.IsFeatureEnabledAsync(VerticalProfileGuard.MarketplaceOrdersFeature, ct))
        {
            return (null, OnlineOrderStatusUpdateResult.Fail(
                ProfileEndpointDisabledException.Code,
                ProfileEndpointDisabledException.DefaultMessage));
        }

        var venueId = integration.ExternalLocationId?.Trim() ?? string.Empty;
        var access = venueId.Length == 0
            ? null
            : await _tokens.GetAccessTokenAsync(integration.TenantId, venueId, ct);
        if (string.IsNullOrWhiteSpace(access))
        {
            await RememberAsync(db, integration, "Wolt access token is not available.", ct);
            return (null, OnlineOrderStatusUpdateResult.Fail(TokenCode, "Wolt access token is not available."));
        }

        var fetched = await _wolt.GetOrderAsync(access, integration.ExternalOrderId, ct);
        if (!fetched.Succeeded)
        {
            await RememberAsync(db, integration, "Wolt order could not be fetched.", ct);
            return (null, OnlineOrderStatusUpdateResult.Fail(RejectedCode, "Wolt order could not be fetched."));
        }

        var resolved = ReadSelfDelivery(fetched.Body);
        integration.WoltSelfDelivery = resolved == true;
        return (integration.WoltSelfDelivery, null);
    }

    private async Task<bool> AlreadyInTargetStateAsync(
        string accessToken,
        string orderId,
        string action,
        int statusCode,
        CancellationToken ct)
    {
        if (statusCode != StatusCodes.Status409Conflict)
            return false;

        var fetched = await _wolt.GetOrderAsync(accessToken, orderId, ct);
        if (!fetched.Succeeded)
            return false;

        var current = WoltOrderMapper.ReadStatus(fetched.Body);
        var target = action switch
        {
            "accept" => "PRODUCTION",
            "ready" => "READY",
            "delivered" => "DELIVERED",
            _ => null,
        };
        return target is not null && string.Equals(current, target, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ActionFor(string current, string next)
    {
        var from = current.Trim().ToLowerInvariant();
        var to = next.Trim().ToLowerInvariant();
        if (from == OnlineOrderStatuses.Pending && to == OnlineOrderStatuses.Accepted)
            return "accept";
        if (from == OnlineOrderStatuses.Preparing && to == OnlineOrderStatuses.Ready)
            return "ready";
        if (from == OnlineOrderStatuses.Ready && to == OnlineOrderStatuses.Completed)
            return "delivered";
        return null;
    }

    private static bool? ReadSelfDelivery(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("delivery", out var delivery)
                || delivery.ValueKind != JsonValueKind.Object)
                return null;
            return WoltOrderMapper.ReadSelfDeliveryFlag(delivery);
        }
        catch (JsonException)
        {
            return null;
        }
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
