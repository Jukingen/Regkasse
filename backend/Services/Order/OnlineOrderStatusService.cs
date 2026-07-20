using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Online-order status transitions with append-only history + notifications + loyalty hook.
/// Status-only fulfillment — does not create POS carts, TSE signatures, or fiscal receipts.
/// Prefer this over inventing a parallel generic OrderTrackingService.
/// </summary>
public sealed class OnlineOrderStatusService : IOnlineOrderStatusService
{
    public const string OrderNotFoundCode = "ONLINE_ORDER_NOT_FOUND";
    public const string InvalidStatusCode = "ONLINE_ORDER_STATUS_INVALID";
    public const string InvalidTransitionCode = "ONLINE_ORDER_STATUS_TRANSITION_INVALID";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [OnlineOrderStatuses.Pending] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                OnlineOrderStatuses.Accepted,
                OnlineOrderStatuses.Cancelled,
            },
            [OnlineOrderStatuses.Accepted] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                OnlineOrderStatuses.Preparing,
                OnlineOrderStatuses.Cancelled,
            },
            [OnlineOrderStatuses.Preparing] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                OnlineOrderStatuses.Ready,
                OnlineOrderStatuses.Cancelled,
            },
            [OnlineOrderStatuses.Ready] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                OnlineOrderStatuses.Completed,
                OnlineOrderStatuses.Cancelled,
            },
            [OnlineOrderStatuses.Completed] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [OnlineOrderStatuses.Cancelled] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IOnlineOrderNotificationService _notifications;
    private readonly IOnlineOrderLoyaltyService _loyalty;
    private readonly TimeProvider _time;

    public OnlineOrderStatusService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor,
        IOnlineOrderNotificationService notifications,
        IOnlineOrderLoyaltyService loyalty,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _notifications = notifications;
        _loyalty = loyalty;
        _time = time;
    }

    /// <summary>Next forward step in the kitchen lifecycle, or null when terminal / unknown.</summary>
    public static string? GetNextForwardStatus(string currentStatus)
    {
        var current = (currentStatus ?? string.Empty).Trim().ToLowerInvariant();
        return current switch
        {
            OnlineOrderStatuses.Pending => OnlineOrderStatuses.Accepted,
            OnlineOrderStatuses.Accepted => OnlineOrderStatuses.Preparing,
            OnlineOrderStatuses.Preparing => OnlineOrderStatuses.Ready,
            OnlineOrderStatuses.Ready => OnlineOrderStatuses.Completed,
            _ => null,
        };
    }

    public static bool IsTransitionAllowed(string fromStatus, string toStatus)
    {
        var from = (fromStatus ?? string.Empty).Trim().ToLowerInvariant();
        var to = (toStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!AllowedTransitions.TryGetValue(from, out var allowed))
            return false;
        return allowed.Contains(to);
    }

    public async Task<OnlineOrderStatusUpdateResult> UpdateStatusAsync(
        Guid onlineOrderId,
        string newStatus,
        string? actorUserId = null,
        CancellationToken ct = default)
    {
        var status = (newStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(status) || !OnlineOrderStatuses.All.Contains(status))
        {
            return OnlineOrderStatusUpdateResult.Fail(InvalidStatusCode, "Invalid order status.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == onlineOrderId, ct);
        if (order is null)
            return OnlineOrderStatusUpdateResult.Fail(OrderNotFoundCode, "Online order not found.");

        if (_tenantAccessor.TenantId is Guid ambient
            && ambient != Guid.Empty
            && order.TenantId != ambient)
        {
            return OnlineOrderStatusUpdateResult.Fail(OrderNotFoundCode, "Online order not found.");
        }

        if (string.Equals(order.OrderStatus, status, StringComparison.OrdinalIgnoreCase))
            return OnlineOrderStatusUpdateResult.Success(order);

        if (!IsTransitionAllowed(order.OrderStatus, status))
        {
            return OnlineOrderStatusUpdateResult.Fail(
                InvalidTransitionCode,
                $"Cannot change status from '{order.OrderStatus}' to '{status}'.");
        }

        var previous = order.OrderStatus;
        var now = _time.GetUtcNow().UtcDateTime;
        order.OrderStatus = status;
        order.UpdatedAt = now;

        if (string.Equals(status, OnlineOrderStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
            order.AcceptedAt ??= now;
        else if (string.Equals(status, OnlineOrderStatuses.Ready, StringComparison.OrdinalIgnoreCase))
            order.ReadyAt ??= now;
        else if (string.Equals(status, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            order.CompletedAt ??= now;

        db.OnlineOrderStatusChanges.Add(new OnlineOrderStatusChange
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            OnlineOrderId = order.Id,
            FromStatus = previous,
            ToStatus = status,
            ChangedAt = now,
            ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId.Trim()
        });

        await db.SaveChangesAsync(ct);
        await _notifications.NotifyStatusChangedAsync(order, previous, ct);

        if (string.Equals(status, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _loyalty.TryEarnOnCompletedAsync(order, ct);
            }
            catch
            {
                // Loyalty must never fail the status transition.
            }
        }

        return OnlineOrderStatusUpdateResult.Success(order);
    }
}
