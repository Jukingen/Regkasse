using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Kitchen;

public interface IKitchenOrderService
{
    Task<IReadOnlyList<KitchenOrderDto>> ListAsync(
        KitchenOrderStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);

    Task<KitchenOrderWriteResult> CreateAsync(
        CreateKitchenOrderRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);

    Task<KitchenOrderWriteResult> UpdateStatusAsync(
        Guid id,
        KitchenOrderStatus status,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);

    Task<KitchenOrderWriteResult> UpdateItemStatusAsync(
        Guid id,
        Guid itemId,
        KitchenOrderItemStatus status,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);

    Task<KitchenOrderWriteResult> CancelAsync(
        Guid id,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken);
}

public sealed class KitchenOrderService : IKitchenOrderService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogService _auditLog;
    private readonly IKitchenOrderBroadcaster _hub;
    private readonly ILogger<KitchenOrderService> _logger;

    public KitchenOrderService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogService auditLog,
        IKitchenOrderBroadcaster hub,
        ILogger<KitchenOrderService> logger)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _auditLog = auditLog;
        _hub = hub;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KitchenOrderDto>> ListAsync(
        KitchenOrderStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return [];

        var query = _db.KitchenOrders
            .AsNoTracking()
            .Include(row => row.Items)
            .Where(row => row.TenantId == tenantId);
        if (status is KitchenOrderStatus filter)
            query = query.Where(row => row.Status == filter);
        if (fromUtc is DateTime from)
            query = query.Where(row => row.CreatedAtUtc >= from.ToUniversalTime());
        if (toUtc is DateTime to)
            query = query.Where(row => row.CreatedAtUtc <= to.ToUniversalTime());

        var rows = await query
            .OrderByDescending(row => row.Priority)
            .ThenBy(row => row.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<KitchenOrderWriteResult> CreateAsync(
        CreateKitchenOrderRequest request,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return new KitchenOrderWriteResult.NotFound();
        if (request.CashRegisterId == Guid.Empty)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.CashRegisterRequired,
                "Cash register is required.");
        }

        var register = await _db.CashRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == request.CashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (register is null || register.TenantId != tenantId)
            return new KitchenOrderWriteResult.NotFound();

        var lines = await ResolveItemsAsync(request, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.Empty,
                "Kitchen order has no items.");
        }

        Guid? cartId = null;
        if (request.CartId is Guid requestedCart && requestedCart != Guid.Empty)
        {
            var cartExists = await _db.Carts
                .AsNoTracking()
                .AnyAsync(cart => cart.Id == requestedCart, cancellationToken)
                .ConfigureAwait(false);
            if (cartExists)
                cartId = requestedCart;
        }

        var now = DateTime.UtcNow;
        var actor = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        var row = new KitchenOrder
        {
            TenantId = tenantId,
            CartId = cartId,
            TableNumber = NormalizeTable(request.TableNumber),
            CashRegisterId = request.CashRegisterId,
            CreatedByUserId = actor,
            Status = KitchenOrderStatus.Pending,
            Priority = request.Priority,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        foreach (var line in lines)
        {
            row.Items.Add(new KitchenOrderItem
            {
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                Notes = line.Notes,
                Status = KitchenOrderItemStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        _db.KitchenOrders.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = Map(row);
        await AuditAsync(
                "KITCHEN_ORDER_CREATED",
                AuditEventType.KitchenOrderCreated,
                row,
                actor,
                actorRole,
                oldValues: null,
                newValues: new { row.Id, itemCount = row.Items.Count, row.TableNumber },
                cancellationToken)
            .ConfigureAwait(false);
        await _hub.OrderCreatedAsync(dto, cancellationToken).ConfigureAwait(false);
        return new KitchenOrderWriteResult.Ok(dto, StatusCodes.Status201Created);
    }

    public async Task<KitchenOrderWriteResult> UpdateStatusAsync(
        Guid id,
        KitchenOrderStatus status,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return new KitchenOrderWriteResult.NotFound();

        var blocked = GuardMutable(row);
        if (blocked is not null)
            return blocked;

        if (status == KitchenOrderStatus.Cancelled)
            return await CancelAsync(id, actorUserId, actorRole, cancellationToken).ConfigureAwait(false);

        if (!Enum.IsDefined(status) || status == KitchenOrderStatus.Cancelled)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.InvalidStatus,
                "Invalid kitchen order status.");
        }

        var previous = row.Status;
        ApplyOrderStatus(row, status, DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = Map(row);
        await AuditAsync(
                "KITCHEN_ORDER_STATUS_CHANGED",
                AuditEventType.KitchenOrderStatusChanged,
                row,
                actorUserId,
                actorRole,
                oldValues: new { status = previous.ToString() },
                newValues: new { status = row.Status.ToString() },
                cancellationToken)
            .ConfigureAwait(false);
        await _hub.OrderStatusChangedAsync(dto, cancellationToken).ConfigureAwait(false);
        return new KitchenOrderWriteResult.Ok(dto, StatusCodes.Status200OK);
    }

    public async Task<KitchenOrderWriteResult> UpdateItemStatusAsync(
        Guid id,
        Guid itemId,
        KitchenOrderItemStatus status,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return new KitchenOrderWriteResult.NotFound();

        var blocked = GuardMutable(row);
        if (blocked is not null)
            return blocked;

        var item = row.Items.FirstOrDefault(line => line.Id == itemId);
        if (item is null)
            return new KitchenOrderWriteResult.NotFound();
        if (!Enum.IsDefined(status))
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.InvalidStatus,
                "Invalid kitchen item status.");
        }

        var previous = item.Status;
        item.Status = status;
        item.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = item.UpdatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = Map(row);
        await AuditAsync(
                "KITCHEN_ORDER_STATUS_CHANGED",
                AuditEventType.KitchenOrderStatusChanged,
                row,
                actorUserId,
                actorRole,
                oldValues: new { itemId, status = previous.ToString() },
                newValues: new { itemId, status = item.Status.ToString() },
                cancellationToken)
            .ConfigureAwait(false);
        await _hub.ItemStatusChangedAsync(dto, item.Id, cancellationToken).ConfigureAwait(false);
        return new KitchenOrderWriteResult.Ok(dto, StatusCodes.Status200OK);
    }

    public async Task<KitchenOrderWriteResult> CancelAsync(
        Guid id,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return new KitchenOrderWriteResult.NotFound();
        if (row.Status == KitchenOrderStatus.Cancelled)
            return new KitchenOrderWriteResult.Ok(Map(row), StatusCodes.Status200OK);
        if (row.Status == KitchenOrderStatus.Served)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.AlreadyServed,
                "Served kitchen orders cannot be cancelled.");
        }

        var previous = row.Status;
        row.Status = KitchenOrderStatus.Cancelled;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = Map(row);
        await AuditAsync(
                "KITCHEN_ORDER_CANCELLED",
                AuditEventType.KitchenOrderCancelled,
                row,
                actorUserId,
                actorRole,
                oldValues: new { status = previous.ToString() },
                newValues: new { status = row.Status.ToString() },
                cancellationToken)
            .ConfigureAwait(false);
        await _hub.OrderStatusChangedAsync(dto, cancellationToken).ConfigureAwait(false);
        return new KitchenOrderWriteResult.Ok(dto, StatusCodes.Status200OK);
    }

    private async Task<KitchenOrder?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return null;
        return await _db.KitchenOrders
            .Include(row => row.Items)
            .FirstOrDefaultAsync(row => row.Id == id && row.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<(Guid? ProductId, string ProductName, int Quantity, string? Notes)>> ResolveItemsAsync(
        CreateKitchenOrderRequest request,
        CancellationToken cancellationToken)
    {
        var lines = new List<(Guid? ProductId, string ProductName, int Quantity, string? Notes)>();
        foreach (var item in request.Items ?? [])
        {
            var quantity = Math.Max(1, item.Quantity);
            var name = item.ProductName?.Trim();
            Guid? productId = item.ProductId is Guid id && id != Guid.Empty ? id : null;
            if (string.IsNullOrWhiteSpace(name) && productId is Guid lookup)
            {
                name = await _db.Products
                    .AsNoTracking()
                    .Where(product => product.Id == lookup)
                    .Select(product => product.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(name))
                continue;
            lines.Add((productId, name.Trim(), quantity, string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim()));
        }

        if (lines.Count > 0 || request.CartId is not Guid cartId || cartId == Guid.Empty)
            return lines;

        var cartItems = await _db.CartItems
            .AsNoTracking()
            .Where(item => item.Cart.Id == cartId)
            .Select(item => new { item.ProductId, item.Quantity, item.Notes })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var item in cartItems)
        {
            var name = await _db.Products
                .AsNoTracking()
                .Where(product => product.Id == item.ProductId)
                .Select(product => product.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(name))
                continue;
            lines.Add((item.ProductId, name, Math.Max(1, item.Quantity), item.Notes));
        }

        return lines;
    }

    private static KitchenOrderWriteResult? GuardMutable(KitchenOrder row)
    {
        if (row.Status == KitchenOrderStatus.Served)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.AlreadyServed,
                "Served kitchen orders cannot change status.");
        }

        if (row.Status == KitchenOrderStatus.Cancelled)
        {
            return new KitchenOrderWriteResult.Error(
                StatusCodes.Status400BadRequest,
                KitchenOrderErrorCodes.Cancelled,
                "Cancelled kitchen orders cannot change status.");
        }

        return null;
    }

    private static void ApplyOrderStatus(KitchenOrder row, KitchenOrderStatus status, DateTime now)
    {
        row.Status = status;
        row.UpdatedAtUtc = now;
        if (status == KitchenOrderStatus.Ready)
            row.ReadyAtUtc ??= now;
        if (status == KitchenOrderStatus.Served)
        {
            row.ReadyAtUtc ??= now;
            row.ServedAtUtc ??= now;
        }
    }

    private static string? NormalizeTable(string? tableNumber)
    {
        var trimmed = tableNumber?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;
        return trimmed.Length <= 16 ? trimmed : trimmed[..16];
    }

    private static KitchenOrderDto Map(KitchenOrder row) =>
        new()
        {
            Id = row.Id,
            TenantId = row.TenantId,
            CartId = row.CartId,
            TableNumber = row.TableNumber,
            CashRegisterId = row.CashRegisterId,
            CreatedByUserId = row.CreatedByUserId,
            Status = row.Status,
            Priority = row.Priority,
            Notes = row.Notes,
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc,
            ReadyAtUtc = row.ReadyAtUtc,
            ServedAtUtc = row.ServedAtUtc,
            Items = row.Items
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new KitchenOrderItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    Notes = item.Notes,
                    Status = item.Status,
                    CreatedAtUtc = item.CreatedAtUtc,
                    UpdatedAtUtc = item.UpdatedAtUtc,
                })
                .ToList(),
        };

    private async Task AuditAsync(
        string action,
        AuditEventType actionType,
        KitchenOrder row,
        string? actorUserId,
        string? actorRole,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLog.LogSystemOperationAsync(
                    action: action,
                    entityType: nameof(KitchenOrder),
                    userId: actorUserId ?? "system",
                    userRole: actorRole ?? "Cashier",
                    description: action,
                    status: AuditLogStatus.Success,
                    correlationIdOverride: Guid.NewGuid().ToString("N"),
                    actionType: actionType,
                    entityId: row.Id,
                    tenantId: row.TenantId,
                    oldValues: oldValues,
                    newValues: newValues)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kitchen order audit failed for {Action} {OrderId}", action, row.Id);
        }
    }
}
