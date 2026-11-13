using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Online-order notifications: customer email/push + FA activity (SSE / optional ops email).
/// Does not replace fiscal payment alerts.
/// </summary>
public sealed class OnlineOrderNotificationService : IOnlineOrderNotificationService
{
    public const int DefaultEstimatedMinutes = 20;

    private readonly IActivityEventPublisher _activity;
    private readonly IOnlineOrderCustomerEmailService _customerEmail;
    private readonly IOnlineOrderPushSender _push;
    private readonly ILogger<OnlineOrderNotificationService> _logger;

    public OnlineOrderNotificationService(
        IActivityEventPublisher activity,
        IOnlineOrderCustomerEmailService customerEmail,
        IOnlineOrderPushSender push,
        ILogger<OnlineOrderNotificationService> logger)
    {
        _activity = activity;
        _customerEmail = customerEmail;
        _push = push;
        _logger = logger;
    }

    public async Task SendOrderConfirmationAsync(
        OnlineOrder order,
        string? actorUserId = null,
        CancellationToken ct = default)
    {
        await TrySendCustomerConfirmationEmailAsync(order, ct);
        await TrySendCustomerPushAsync(
            order,
            title: "Ihre Bestellung wurde bestätigt!",
            body: $"Bestellung {order.OrderNumber} wird vorbereitet.",
            ct);
        await NotifyStaffConfirmationAsync(order, actorUserId, ct);
    }

    public async Task NotifyPaidAsync(OnlineOrder order, CancellationToken ct = default)
    {
        try
        {
            await _activity.TryPublishAsync(
                order.TenantId,
                ActivityEventType.OnlineOrderPaid,
                new
                {
                    OnlineOrderId = order.Id.ToString("D"),
                    OrderNumber = order.OrderNumber,
                    Total = order.Total.ToString("0.00"),
                    Currency = "EUR",
                    PaymentIntentId = order.StripePaymentIntentId,
                },
                dedupKey: $"online_order_paid_{order.Id:D}",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish OnlineOrderPaid for {OrderId}", order.Id);
        }
    }

    public async Task NotifyStatusChangedAsync(
        OnlineOrder order,
        string previousStatus,
        CancellationToken ct = default)
    {
        try
        {
            await _activity.TryPublishAsync(
                order.TenantId,
                ActivityEventType.OnlineOrderStatusChanged,
                new
                {
                    OnlineOrderId = order.Id.ToString("D"),
                    OrderNumber = order.OrderNumber,
                    PreviousStatus = previousStatus,
                    OrderStatus = order.OrderStatus,
                    NewStatus = order.OrderStatus,
                },
                dedupKey: $"online_order_status_{order.Id:D}_{order.OrderStatus}",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish OnlineOrderStatusChanged for {OrderId}", order.Id);
        }

        // Customer channels for key kitchen milestones
        if (string.Equals(order.OrderStatus, OnlineOrderStatuses.Ready, StringComparison.OrdinalIgnoreCase))
        {
            await TrySendCustomerStatusEmailAsync(
                order,
                "Bestellung bereit",
                $"Ihre Bestellung {order.OrderNumber} ist bereit.",
                ct);
            await TrySendCustomerPushAsync(
                order,
                "Ihre Bestellung ist bereit!",
                $"Bestellung {order.OrderNumber} kann abgeholt werden.",
                ct);
        }
        else if (string.Equals(order.OrderStatus, OnlineOrderStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
                 && !string.Equals(previousStatus, OnlineOrderStatuses.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            // Accept via status PATCH (without POS push) still confirms the customer.
            await SendOrderConfirmationAsync(order, actorUserId: null, ct);
        }
    }

    private async Task TrySendCustomerConfirmationEmailAsync(OnlineOrder order, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return;

        try
        {
            await _customerEmail.TrySendOrderConfirmationAsync(
                new OnlineOrderCustomerEmailRequest(
                    order.CustomerEmail,
                    order.CustomerName,
                    order.OrderNumber,
                    order.Total,
                    "EUR",
                    order.OrderType,
                    DefaultEstimatedMinutes),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer confirmation email failed for {OrderId}", order.Id);
        }
    }

    private async Task TrySendCustomerStatusEmailAsync(
        OnlineOrder order,
        string headline,
        string body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return;

        try
        {
            await _customerEmail.TrySendOrderStatusAsync(
                new OnlineOrderCustomerEmailRequest(
                    order.CustomerEmail,
                    order.CustomerName,
                    order.OrderNumber,
                    order.Total,
                    "EUR",
                    order.OrderType,
                    DefaultEstimatedMinutes),
                headline,
                body,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer status email failed for {OrderId}", order.Id);
        }
    }

    private async Task TrySendCustomerPushAsync(
        OnlineOrder order,
        string title,
        string body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerDeviceToken))
            return;

        try
        {
            await _push.TrySendAsync(
                order.CustomerDeviceToken,
                title,
                body,
                new Dictionary<string, string>
                {
                    ["onlineOrderId"] = order.Id.ToString("D"),
                    ["orderNumber"] = order.OrderNumber,
                    ["orderStatus"] = order.OrderStatus,
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer push failed for {OrderId}", order.Id);
        }
    }

    private async Task NotifyStaffConfirmationAsync(
        OnlineOrder order,
        string? actorUserId,
        CancellationToken ct)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(order.PosCartId))
            {
                await _activity.TryPublishAsync(
                    order.TenantId,
                    ActivityEventType.OnlineOrderPushedToPos,
                    new
                    {
                        OnlineOrderId = order.Id.ToString("D"),
                        OrderNumber = order.OrderNumber,
                        CustomerName = order.CustomerName,
                        OrderType = order.OrderType,
                        PosCartId = order.PosCartId,
                        Total = order.Total,
                        Source = order.Source,
                    },
                    actorUserId: actorUserId,
                    dedupKey: $"online_order_pushed_{order.Id:D}",
                    cancellationToken: ct);
            }
            else
            {
                await _activity.TryPublishAsync(
                    order.TenantId,
                    ActivityEventType.OnlineOrderConfirmed,
                    new
                    {
                        OnlineOrderId = order.Id.ToString("D"),
                        OrderNumber = order.OrderNumber,
                        CustomerName = order.CustomerName,
                        OrderType = order.OrderType,
                        Total = order.Total.ToString("0.00"),
                        Currency = "EUR",
                        Source = order.Source,
                    },
                    actorUserId: actorUserId,
                    dedupKey: $"online_order_confirmed_{order.Id:D}",
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish staff confirmation for {OrderId}", order.Id);
        }
    }
}
