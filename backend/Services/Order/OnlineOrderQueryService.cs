using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

/// <summary>Tenant-scoped online order reads for FA order management + public status lookup.</summary>
public sealed class OnlineOrderQueryService : IOnlineOrderQueryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public OnlineOrderQueryService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<OnlineOrderListResponseDto> ListAsync(
        string? status = null,
        int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var pending = await db.OnlineOrders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OnlineOrderStatuses.Pending, ct);
        var accepted = await db.OnlineOrders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OnlineOrderStatuses.Accepted, ct);
        var preparing = await db.OnlineOrders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OnlineOrderStatuses.Preparing, ct);
        var ready = await db.OnlineOrders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OnlineOrderStatuses.Ready, ct);
        var completed = await db.OnlineOrders.AsNoTracking()
            .CountAsync(o => o.OrderStatus == OnlineOrderStatuses.Completed, ct);

        var query = db.OnlineOrders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Modifiers)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)
            && OnlineOrderStatuses.All.Contains(status.Trim()))
        {
            var normalized = status.Trim();
            query = query.Where(o => o.OrderStatus == normalized);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return new OnlineOrderListResponseDto
        {
            Pending = pending,
            Accepted = accepted,
            Preparing = preparing,
            Ready = ready,
            Completed = completed,
            Orders = orders.Select(o => Map(o)).ToList()
        };
    }

    public async Task<OnlineOrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var order = await db.OnlineOrders.AsNoTracking()
            .Include(o => o.Items)
            .ThenInclude(i => i.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return null;

        var history = await LoadHistoryAsync(db, order.Id, ct);
        return Map(order, history);
    }

    public async Task<OnlineOrderAnalyticsDto> GetAnalyticsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var to = toUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var from = fromUtc?.ToUniversalTime() ?? to.AddDays(-30);
        if (from > to)
            (from, to) = (to, from);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var orders = await db.OnlineOrders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
            .Select(o => new
            {
                o.OrderStatus,
                o.Source,
                o.OrderType,
                o.Total,
                o.PaymentStatus,
                o.AcceptedAt,
                o.ReadyAt
            })
            .ToListAsync(ct);

        var paidOrCash = orders
            .Where(o =>
                string.Equals(o.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.OrderStatus, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var revenue = paidOrCash
            .Where(o => !string.Equals(o.OrderStatus, OnlineOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            .Sum(o => o.Total);

        var completedCount = orders.Count(o =>
            string.Equals(o.OrderStatus, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase));

        var acceptReady = orders
            .Where(o => o.AcceptedAt is not null && o.ReadyAt is not null && o.ReadyAt >= o.AcceptedAt)
            .Select(o => (o.ReadyAt!.Value - o.AcceptedAt!.Value).TotalMinutes)
            .ToList();

        return new OnlineOrderAnalyticsDto
        {
            FromUtc = from,
            ToUtc = to,
            TotalOrders = orders.Count,
            Pending = orders.Count(o =>
                string.Equals(o.OrderStatus, OnlineOrderStatuses.Pending, StringComparison.OrdinalIgnoreCase)),
            Completed = completedCount,
            Cancelled = orders.Count(o =>
                string.Equals(o.OrderStatus, OnlineOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)),
            Revenue = revenue,
            AverageOrderValue = orders.Count == 0 ? 0 : Math.Round(orders.Average(o => o.Total), 2),
            AvgAcceptToReadyMinutes = acceptReady.Count == 0
                ? null
                : Math.Round(acceptReady.Average(), 1),
            ByStatus = orders.GroupBy(o => o.OrderStatus)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            BySource = orders.GroupBy(o => o.Source)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase),
            ByOrderType = orders.GroupBy(o => o.OrderType)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task<PublicOnlineOrderStatusDto?> GetPublicStatusAsync(
        string tenantSlug,
        string orderNumber,
        string? customerPhone = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug) || string.IsNullOrWhiteSpace(orderNumber))
            return null;

        var slug = tenantSlug.Trim().ToLowerInvariant();
        var number = orderNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Slug.ToLower() == slug && t.IsActive && t.DeletedAtUtc == null,
                ct);
        if (tenant is null)
            return null;

        var numberLower = number.ToLowerInvariant();
        var match = await db.OnlineOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                o => o.TenantId == tenant.Id
                     && o.OrderNumber.ToLower() == numberLower,
                ct);
        if (match is null)
            return null;

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            var requested = DigitsOnly(customerPhone);
            var stored = DigitsOnly(match.CustomerPhone);
            var phoneOk = requested.Length > 0
                && stored.Length > 0
                && (string.Equals(stored, requested, StringComparison.Ordinal)
                    || stored.EndsWith(requested, StringComparison.Ordinal));
            if (!phoneOk)
                return null;
        }

        var history = await db.OnlineOrderStatusChanges.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.OnlineOrderId == match.Id)
            .OrderBy(c => c.ChangedAt)
            .Select(c => new OnlineOrderStatusChangeDto
            {
                Id = c.Id,
                FromStatus = c.FromStatus,
                ToStatus = c.ToStatus,
                ChangedAt = c.ChangedAt
            })
            .ToListAsync(ct);

        return new PublicOnlineOrderStatusDto
        {
            OrderNumber = match.OrderNumber,
            OrderStatus = match.OrderStatus,
            OrderType = match.OrderType,
            PaymentStatus = match.PaymentStatus,
            Total = match.Total,
            Currency = "EUR",
            CreatedAt = match.CreatedAt,
            AcceptedAt = match.AcceptedAt,
            ReadyAt = match.ReadyAt,
            CompletedAt = match.CompletedAt,
            PaidAt = match.PaidAt,
            CustomerDisplayName = MaskCustomerName(match.CustomerName),
            StatusHistory = history
        };
    }

    private static async Task<IReadOnlyList<OnlineOrderStatusChangeDto>> LoadHistoryAsync(
        AppDbContext db,
        Guid orderId,
        CancellationToken ct) =>
        await db.OnlineOrderStatusChanges.AsNoTracking()
            .Where(c => c.OnlineOrderId == orderId)
            .OrderBy(c => c.ChangedAt)
            .Select(c => new OnlineOrderStatusChangeDto
            {
                Id = c.Id,
                FromStatus = c.FromStatus,
                ToStatus = c.ToStatus,
                ChangedAt = c.ChangedAt
            })
            .ToListAsync(ct);

    private static string DigitsOnly(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string? MaskCustomerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length <= 1 ? parts[0] : $"{parts[0][0]}.";
        var last = parts[^1];
        var lastInitial = last.Length > 0 ? $"{last[0]}." : "";
        return $"{parts[0]} {lastInitial}".Trim();
    }

    internal static OnlineOrderDto Map(
        OnlineOrder order,
        IReadOnlyList<OnlineOrderStatusChangeDto>? history = null) =>
        new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,
            OrderType = order.OrderType,
            TableNumber = order.TableNumber,
            DeliveryAddress = order.DeliveryAddress,
            Subtotal = order.Subtotal,
            Tax = order.Tax,
            Total = order.Total,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            PaidAt = order.PaidAt,
            StripePaymentIntentId = order.StripePaymentIntentId,
            OrderStatus = order.OrderStatus,
            Source = order.Source,
            CreatedAt = order.CreatedAt,
            AcceptedAt = order.AcceptedAt,
            ReadyAt = order.ReadyAt,
            CompletedAt = order.CompletedAt,
            Notes = order.Notes,
            PosCartId = order.PosCartId,
            CustomerId = order.CustomerId,
            StatusHistory = history ?? Array.Empty<OnlineOrderStatusChangeDto>(),
            Items = order.Items
                .OrderBy(i => i.ProductName)
                .Select(i => new OnlineOrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    Price = i.Price,
                    Total = i.Total,
                    Modifiers = i.Modifiers
                        .Select(m => new OnlineOrderItemModifierDto
                        {
                            Id = m.Id,
                            ModifierId = m.ModifierId,
                            Name = m.Name,
                            Price = m.Price,
                            Quantity = m.Quantity
                        })
                        .ToList()
                })
                .ToList()
        };
}
