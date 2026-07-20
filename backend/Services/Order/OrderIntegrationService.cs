using System.Globalization;
using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Bridges customer <see cref="OnlineOrder"/> intake into the POS working set (<see cref="Cart"/>).
/// Does not create fiscal receipts — payment/TSE remains on the POS payment path.
/// </summary>
public sealed class OrderIntegrationService : IOrderIntegrationService
{
    public const string OrderNotFoundCode = "ONLINE_ORDER_NOT_FOUND";
    public const string OrderCancelledCode = "ONLINE_ORDER_CANCELLED";
    public const string OrderCompletedCode = "ONLINE_ORDER_COMPLETED";
    public const string ClaimingUserRequiredCode = "CLAIMING_USER_REQUIRED";
    public const string ClaimingUserNotFoundCode = "CLAIMING_USER_NOT_FOUND";
    public const string EmptyItemsCode = "ONLINE_ORDER_EMPTY";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IOnlineOrderNotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<OrderIntegrationService> _logger;

    public OrderIntegrationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenantAccessor,
        IOnlineOrderNotificationService notifications,
        TimeProvider time,
        ILogger<OrderIntegrationService> logger)
    {
        _dbFactory = dbFactory;
        _tenantAccessor = tenantAccessor;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    public async Task<OrderIntegrationResult> PushOrderToPosAsync(
        Guid onlineOrderId,
        string claimingUserId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await LoadOrderAsync(db, onlineOrderId, ct);
        if (order is null)
            return OrderIntegrationResult.Fail(OrderNotFoundCode, "Online order not found.");

        return await PushCoreAsync(db, order, claimingUserId, ct);
    }

    public async Task<OrderIntegrationResult> PushOrderToPosAsync(
        OnlineOrder order,
        string claimingUserId,
        CancellationToken ct = default)
    {
        if (order is null)
            return OrderIntegrationResult.Fail(OrderNotFoundCode, "Online order not found.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tracked = await LoadOrderAsync(db, order.Id, ct);
        if (tracked is null)
            return OrderIntegrationResult.Fail(OrderNotFoundCode, "Online order not found.");

        return await PushCoreAsync(db, tracked, claimingUserId, ct);
    }

    private async Task<OnlineOrder?> LoadOrderAsync(AppDbContext db, Guid onlineOrderId, CancellationToken ct)
    {
        // Ignore ambient filter so missing tenant accessor (tests / background) still loads by id;
        // enforce tenant match when ambient tenant is set (404 semantics for cross-tenant).
        var order = await db.OnlineOrders
            .IgnoreQueryFilters()
            .Include(o => o.Items)
            .ThenInclude(i => i.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == onlineOrderId, ct);

        if (order is null)
            return null;

        if (_tenantAccessor.TenantId is Guid ambient
            && ambient != Guid.Empty
            && order.TenantId != ambient)
        {
            return null;
        }

        return order;
    }

    private async Task<OrderIntegrationResult> PushCoreAsync(
        AppDbContext db,
        OnlineOrder order,
        string claimingUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(claimingUserId))
            return OrderIntegrationResult.Fail(ClaimingUserRequiredCode, "Claiming POS user id is required.");

        if (!string.IsNullOrWhiteSpace(order.PosCartId))
        {
            _logger.LogInformation(
                "Online order {OrderId} already linked to POS cart {CartId}",
                order.Id,
                order.PosCartId);
            return OrderIntegrationResult.Success(order, order.PosCartId!, alreadyPushed: true);
        }

        if (string.Equals(order.OrderStatus, OnlineOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            return OrderIntegrationResult.Fail(OrderCancelledCode, "Cancelled online orders cannot be pushed to POS.");

        if (string.Equals(order.OrderStatus, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return OrderIntegrationResult.Fail(OrderCompletedCode, "Completed online orders cannot be pushed to POS.");

        if (order.Items is null || order.Items.Count == 0)
            return OrderIntegrationResult.Fail(EmptyItemsCode, "Online order has no items.");

        var userExists = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == claimingUserId, ct);
        if (!userExists)
            return OrderIntegrationResult.Fail(ClaimingUserNotFoundCode, "Claiming POS user not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        var cartId = Guid.NewGuid().ToString("D");
        int? tableNumber = null;
        if (!string.IsNullOrWhiteSpace(order.TableNumber)
            && int.TryParse(order.TableNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTable))
        {
            tableNumber = parsedTable;
        }

        var cart = new Cart
        {
            CartId = cartId,
            UserId = claimingUserId,
            TableNumber = tableNumber,
            WaiterName = "Online",
            Notes = BuildCartNotes(order),
            Status = CartStatus.Active,
            ExpiresAt = now.AddHours(24),
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true
        };

        foreach (var line in order.Items)
        {
            db.CartItems.Add(new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cartId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.Price,
                Notes = BuildItemNotes(line),
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true
            });
        }

        db.Carts.Add(cart);

        order.PosCartId = cartId;
        order.PushedToPosAt = now;
        order.UpdatedAt = now;
        if (string.Equals(order.OrderStatus, OnlineOrderStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            order.OrderStatus = OnlineOrderStatuses.Accepted;
            order.AcceptedAt ??= now;
        }

        await db.SaveChangesAsync(ct);

        await _notifications.SendOrderConfirmationAsync(order, claimingUserId, ct);

        _logger.LogInformation(
            "Online order {OrderId} ({OrderNumber}) pushed to POS cart {CartId} for user {UserId}",
            order.Id,
            order.OrderNumber,
            cartId,
            claimingUserId);

        return OrderIntegrationResult.Success(order, cartId);
    }

    private static string BuildCartNotes(OnlineOrder order)
    {
        var sb = new StringBuilder();
        sb.Append("ONLINE ").Append(order.OrderNumber);
        sb.Append(" | ").Append(order.CustomerName);
        if (!string.IsNullOrWhiteSpace(order.CustomerPhone))
            sb.Append(" | ").Append(order.CustomerPhone);
        if (!string.IsNullOrWhiteSpace(order.DeliveryAddress))
            sb.Append(" | ").Append(order.DeliveryAddress);
        if (!string.IsNullOrWhiteSpace(order.Notes))
            sb.Append(" | ").Append(order.Notes);
        var text = sb.ToString();
        return text.Length <= 500 ? text : text[..500];
    }

    private static string? BuildItemNotes(OnlineOrderItem line)
    {
        if (line.Modifiers is null || line.Modifiers.Count == 0)
            return line.ProductName;

        var mods = string.Join(
            ", ",
            line.Modifiers.Select(m =>
                m.Quantity > 1
                    ? $"{m.Name} x{m.Quantity}"
                    : m.Name));
        var notes = $"{line.ProductName}: {mods}";
        return notes.Length <= 500 ? notes : notes[..500];
    }
}
