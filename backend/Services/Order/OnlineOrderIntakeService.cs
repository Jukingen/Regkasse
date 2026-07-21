using System.Globalization;
using System.Text.RegularExpressions;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Metrics;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntakeService
{
    /// <summary>
    /// Place a customer online order (website/app). Rejects when working hours deny intake.
    /// Never used by POS/FA sales paths.
    /// </summary>
    Task<CreatePublicOnlineOrderResponseDto> CreateAsync(
        CreatePublicOnlineOrderRequestDto request,
        CancellationToken ct = default);
}

/// <summary>
/// Public website/app order intake with working-hours gate (customer surfaces only).
/// </summary>
public sealed class OnlineOrderIntakeService : IOnlineOrderIntakeService
{
    public const string ClosedCode = "ONLINE_ORDERS_CLOSED";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string ValidationCode = "VALIDATION_ERROR";
    public const string ProductsInvalidCode = "PRODUCTS_INVALID";

    private static readonly Regex SafeSlugRegex = new(
        @"^[a-z0-9]([a-z0-9-]{0,62}[a-z0-9])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _time;
    private readonly IBusinessMetricsService _businessMetrics;
    private readonly ILogger<OnlineOrderIntakeService> _logger;

    public OnlineOrderIntakeService(
        IDbContextFactory<AppDbContext> dbFactory,
        TimeProvider time,
        IBusinessMetricsService businessMetrics,
        ILogger<OnlineOrderIntakeService> logger)
    {
        _dbFactory = dbFactory;
        _time = time;
        _businessMetrics = businessMetrics;
        _logger = logger;
    }

    public async Task<CreatePublicOnlineOrderResponseDto> CreateAsync(
        CreatePublicOnlineOrderRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return Fail(ValidationCode, "Request body is required.");
        }

        var slug = request.Tenant?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug) || !SafeSlugRegex.IsMatch(slug))
            return Fail(ValidationCode, "tenant slug is required.");

        var name = request.CustomerName?.Trim() ?? string.Empty;
        var phone = request.CustomerPhone?.Trim() ?? string.Empty;
        if (name.Length < 2)
            return Fail(ValidationCode, "CustomerName is required.");
        if (phone.Count(char.IsDigit) < 6)
            return Fail(ValidationCode, "CustomerPhone must contain at least 6 digits.");

        var items = (request.Items ?? new List<CreatePublicOnlineOrderItemDto>())
            .Where(i => i is not null && i.ProductId != Guid.Empty && i.Quantity > 0)
            .GroupBy(i => i.ProductId)
            .Select(g => new CreatePublicOnlineOrderItemDto
            {
                ProductId = g.Key,
                Quantity = Math.Clamp(g.Sum(x => x.Quantity), 1, 99),
            })
            .ToList();
        if (items.Count == 0)
            return Fail(ValidationCode, "At least one item is required.");

        var orderType = NormalizeOrderType(request.OrderType);
        var paymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        var source = NormalizeSource(request.Source);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Slug.ToLower() == slug && t.IsActive && t.DeletedAtUtc == null)
            .Select(t => new { t.Id, t.Slug })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
            return Fail(TenantNotFoundCode, "Tenant not found.");

        var company = await db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.Id && c.IsActive, ct);

        var hours = company?.WorkingHours ?? WorkingHoursSettings.CreateDefault();
        hours.Normalize();
        var timeZone = string.IsNullOrWhiteSpace(company?.TimeZone)
            ? "Europe/Vienna"
            : company!.TimeZone.Trim();
        var status = hours.EvaluateWebsiteStatus(_time.GetUtcNow(), timeZone);
        if (!status.CanOrder)
        {
            _logger.LogInformation(
                "Online order intake rejected for tenant {Slug}: working hours closed/cutoff ({Message})",
                tenant.Slug,
                status.Message);
            return new CreatePublicOnlineOrderResponseDto
            {
                Succeeded = false,
                Code = ClosedCode,
                Error = status.Message,
                Message = status.Message,
            };
        }

        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenant.Id && p.IsActive && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Price })
            .ToListAsync(ct);
        if (products.Count != productIds.Count)
            return Fail(ProductsInvalidCode, "One or more products are unavailable.");

        var productMap = products.ToDictionary(p => p.Id);
        var lines = new List<OnlineOrderItem>();
        decimal subtotal = 0m;
        foreach (var item in items)
        {
            var product = productMap[item.ProductId];
            var lineTotal = Math.Round(product.Price * item.Quantity, 2, MidpointRounding.AwayFromZero);
            subtotal += lineTotal;
            lines.Add(new OnlineOrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = item.Quantity,
                Price = product.Price,
                Total = lineTotal,
            });
        }

        // Non-fiscal online orders: tax snapshot 0; total = line subtotal (gross catalog prices).
        var tax = 0m;
        var total = subtotal;
        var now = _time.GetUtcNow().UtcDateTime;
        var orderNumber = await NextOrderNumberAsync(db, tenant.Id, ct);

        var order = new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            OrderNumber = orderNumber,
            CustomerName = name,
            CustomerPhone = phone,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail)
                ? null
                : request.CustomerEmail.Trim(),
            OrderType = orderType,
            DeliveryAddress = string.Equals(orderType, OnlineOrderTypes.Delivery, StringComparison.Ordinal)
                ? request.DeliveryAddress?.Trim()
                : null,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PaymentMethod = paymentMethod,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = source,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            CreatedAt = now,
            Items = lines,
        };

        foreach (var line in lines)
            line.OnlineOrderId = order.Id;

        db.OnlineOrders.Add(order);
        db.OnlineOrderStatusChanges.Add(new OnlineOrderStatusChange
        {
            Id = Guid.NewGuid(),
            OnlineOrderId = order.Id,
            TenantId = tenant.Id,
            FromStatus = string.Empty,
            ToStatus = OnlineOrderStatuses.Pending,
            ChangedAt = now,
            Reason = "Placed via website/app",
        });

        await db.SaveChangesAsync(ct);

        _businessMetrics.RecordOrderCreated();

        _logger.LogInformation(
            "Online order {OrderNumber} created for tenant {Slug} (website intake)",
            order.OrderNumber,
            tenant.Slug);

        return new CreatePublicOnlineOrderResponseDto
        {
            Succeeded = true,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Total = order.Total,
            Message = "Bestellung eingegangen",
        };
    }

    private static async Task<string> NextOrderNumberAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken ct)
    {
        var count = await db.OnlineOrders.IgnoreQueryFilters()
            .CountAsync(o => o.TenantId == tenantId, ct);
        return string.Create(CultureInfo.InvariantCulture, $"ORD-{(count + 1):D3}");
    }

    private static string NormalizeOrderType(string? value)
    {
        var v = (value ?? OnlineOrderTypes.Takeaway).Trim().ToLowerInvariant();
        return OnlineOrderTypes.All.Contains(v) ? v : OnlineOrderTypes.Takeaway;
    }

    private static string NormalizePaymentMethod(string? value)
    {
        var v = (value ?? OnlineOrderPaymentMethods.Cash).Trim().ToLowerInvariant();
        return OnlineOrderPaymentMethods.All.Contains(v) ? v : OnlineOrderPaymentMethods.Cash;
    }

    private static string NormalizeSource(string? value)
    {
        var v = (value ?? OnlineOrderSources.Web).Trim().ToLowerInvariant();
        return OnlineOrderSources.All.Contains(v) ? v : OnlineOrderSources.Web;
    }

    private static CreatePublicOnlineOrderResponseDto Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error,
            Message = error,
        };
}
