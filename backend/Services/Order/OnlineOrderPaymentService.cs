using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Online-order checkout via existing <see cref="IPaymentGateway"/> (Stripe in prod, Mock in Development).
/// Does not touch fiscal <c>PaymentService</c> / RKSV.
/// </summary>
public sealed class OnlineOrderPaymentService : IOnlineOrderPaymentService
{
    public const string OrderNotFoundCode = "ONLINE_ORDER_NOT_FOUND";
    public const string AlreadyPaidCode = "ONLINE_ORDER_ALREADY_PAID";
    public const string InvalidPaymentMethodCode = "ONLINE_ORDER_PAYMENT_METHOD_INVALID";
    public const string InvalidAmountCode = "ONLINE_ORDER_AMOUNT_INVALID";
    public const string GatewayErrorCode = "ONLINE_ORDER_GATEWAY_ERROR";
    public const string IntentNotFoundCode = "ONLINE_ORDER_INTENT_NOT_FOUND";
    public const string PaymentFailedCode = "ONLINE_ORDER_PAYMENT_FAILED";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IPaymentGateway _gateway;
    private readonly IOnlineOrderNotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlineOrderPaymentService> _logger;

    public OnlineOrderPaymentService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor,
        IPaymentGateway gateway,
        IOnlineOrderNotificationService notifications,
        TimeProvider time,
        ILogger<OnlineOrderPaymentService> logger)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _gateway = gateway;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    public async Task<OnlineOrderPaymentResult> CreatePaymentIntentAsync(
        Guid onlineOrderId,
        string? tenantSlug = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await LoadOrderAsync(db, onlineOrderId, tenantSlug, ct);
        if (order is null)
            return OnlineOrderPaymentResult.Fail(OrderNotFoundCode, "Online order not found.");

        if (string.Equals(order.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return OnlineOrderPaymentResult.Fail(AlreadyPaidCode, "Order is already paid.");

        if (!string.Equals(order.PaymentMethod, OnlineOrderPaymentMethods.Online, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.PaymentMethod, OnlineOrderPaymentMethods.Card, StringComparison.OrdinalIgnoreCase))
        {
            return OnlineOrderPaymentResult.Fail(
                InvalidPaymentMethodCode,
                "Payment intent is only available for online/card payment methods.");
        }

        if (order.Total < 0.01m)
            return OnlineOrderPaymentResult.Fail(InvalidAmountCode, "Order total must be greater than zero.");

        // Reuse existing intent client secret path: refresh status if already created.
        if (!string.IsNullOrWhiteSpace(order.StripePaymentIntentId))
        {
            var status = await _gateway.GetPaymentStatusAsync(order.StripePaymentIntentId, ct);
            if (status == PaymentIntentStatus.Succeeded)
            {
                await ApplyPaidAsync(db, order, ct);
                return OnlineOrderPaymentResult.Success(
                    order,
                    order.StripePaymentIntentId,
                    clientSecret: null,
                    _gateway.ProviderName);
            }
        }

        var internalId = Guid.NewGuid();
        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.CreatePaymentIntentAsync(
                new CreatePaymentIntentRequest
                {
                    InternalIntentId = internalId,
                    Amount = order.Total,
                    Currency = "EUR",
                    Description = $"Online order {order.OrderNumber}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["online_order_id"] = order.Id.ToString("D"),
                        ["order_number"] = order.OrderNumber,
                        ["tenant_id"] = order.TenantId.ToString("D"),
                        ["purpose"] = "online_order"
                    }
                },
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway CreatePaymentIntent failed for online order {OrderId}", order.Id);
            return OnlineOrderPaymentResult.Fail(GatewayErrorCode, "Payment gateway is unavailable.");
        }

        if (!gatewayResult.Success || string.IsNullOrWhiteSpace(gatewayResult.PaymentIntentId))
        {
            return OnlineOrderPaymentResult.Fail(
                GatewayErrorCode,
                gatewayResult.ErrorMessage ?? "Payment intent creation failed.");
        }

        order.StripePaymentIntentId = gatewayResult.PaymentIntentId;
        order.PaymentStatus = OnlineOrderPaymentStatuses.Pending;
        order.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return OnlineOrderPaymentResult.Success(
            order,
            gatewayResult.PaymentIntentId,
            gatewayResult.ClientSecret,
            _gateway.ProviderName);
    }

    public async Task<OnlineOrderPaymentResult> ConfirmPaymentAsync(
        string paymentIntentId,
        string? paymentMethodId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return OnlineOrderPaymentResult.Fail(IntentNotFoundCode, "Payment intent id is required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId, ct);
        if (order is null)
            return OnlineOrderPaymentResult.Fail(IntentNotFoundCode, "No online order for this payment intent.");

        if (string.Equals(order.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return OnlineOrderPaymentResult.Success(order, paymentIntentId, null, _gateway.ProviderName);

        PaymentIntentResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.ConfirmPaymentAsync(paymentIntentId, paymentMethodId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway ConfirmPayment failed for {PaymentIntentId}", paymentIntentId);
            return OnlineOrderPaymentResult.Fail(GatewayErrorCode, "Payment gateway is unavailable.");
        }

        if (gatewayResult.Status == PaymentIntentStatus.Succeeded)
        {
            await ApplyPaidAsync(db, order, ct);
            return OnlineOrderPaymentResult.Success(order, paymentIntentId, null, _gateway.ProviderName);
        }

        order.PaymentStatus = OnlineOrderPaymentStatuses.Failed;
        order.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        return OnlineOrderPaymentResult.Fail(
            PaymentFailedCode,
            gatewayResult.ErrorMessage ?? "Payment failed.");
    }

    public async Task<OnlineOrderPaymentResult> MarkPaidFromGatewayAsync(
        string paymentIntentId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return OnlineOrderPaymentResult.Fail(IntentNotFoundCode, "Payment intent id is required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.StripePaymentIntentId == paymentIntentId, ct);
        if (order is null)
            return OnlineOrderPaymentResult.Fail(IntentNotFoundCode, "No online order for this payment intent.");

        if (string.Equals(order.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return OnlineOrderPaymentResult.Success(order, paymentIntentId, null, _gateway.ProviderName);

        await ApplyPaidAsync(db, order, ct);
        return OnlineOrderPaymentResult.Success(order, paymentIntentId, null, _gateway.ProviderName);
    }

    private async Task ApplyPaidAsync(AppDbContext db, OnlineOrder order, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        order.PaymentStatus = OnlineOrderPaymentStatuses.Paid;
        order.PaidAt = now;
        order.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await _notifications.NotifyPaidAsync(order, ct);
    }

    private async Task<OnlineOrder?> LoadOrderAsync(
        AppDbContext db,
        Guid onlineOrderId,
        string? tenantSlug,
        CancellationToken ct)
    {
        var order = await db.OnlineOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == onlineOrderId, ct);
        if (order is null)
            return null;

        if (!string.IsNullOrWhiteSpace(tenantSlug))
        {
            var slug = tenantSlug.Trim().ToLowerInvariant();
            var tenantOk = await db.Tenants.AsNoTracking()
                .IgnoreQueryFilters()
                .AnyAsync(
                    t => t.Id == order.TenantId
                         && t.Slug.ToLower() == slug
                         && t.IsActive
                         && t.DeletedAtUtc == null,
                    ct);
            if (!tenantOk)
                return null;
        }
        else if (_tenantAccessor.TenantId is Guid ambient
                 && ambient != Guid.Empty
                 && order.TenantId != ambient)
        {
            return null;
        }

        return order;
    }
}
