using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntegrationProcessor
{
    /// <summary>
    /// Maps a stored integration row and, when the totals match, pushes a cart.
    /// Does not call <c>PaymentService</c>.
    /// </summary>
    Task ProcessAsync(Guid integrationId, CancellationToken ct = default);
}

public sealed class OnlineOrderIntegrationProcessor : IOnlineOrderIntegrationProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IOrderIntegrationService _carts;
    private readonly IAuditLogService _audit;
    private readonly IActivityEventPublisher _activity;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlineOrderIntegrationProcessor> _logger;

    public OnlineOrderIntegrationProcessor(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        IOrderIntegrationService carts,
        IAuditLogService audit,
        IActivityEventPublisher activity,
        IHostEnvironment environment,
        TimeProvider time,
        ILogger<OnlineOrderIntegrationProcessor> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _carts = carts;
        _audit = audit;
        _activity = activity;
        _environment = environment;
        _time = time;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid integrationId, CancellationToken ct = default)
    {
        if (_environment.IsProduction() && !OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed)
        {
            _logger.LogInformation(
                "Skipping online-order integration {IntegrationId}: signature scheme is not confirmed in Production.",
                integrationId);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var row = await db.OnlineOrderIntegrations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == integrationId && r.IsActive, ct);
        if (row is null)
            return;

        if (row.Status is OnlineOrderIntegrationStatuses.Failed
            or OnlineOrderIntegrationStatuses.CartLinked
            or OnlineOrderIntegrationStatuses.Rejected
            or OnlineOrderIntegrationStatuses.Duplicate)
            return;

        if (row.AttemptCount >= OnlineOrderIntegrationStatuses.MaxAttempts)
        {
            await MarkFailedAsync(db, row, row.LastError ?? "Retry limit reached.", ct);
            return;
        }

        _tenant.TenantId = row.TenantId;

        try
        {
            await ProcessCoreAsync(db, row, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Online-order integration {IntegrationId} attempt failed.",
                row.Id);
            await ScheduleRetryAsync(db, row, "Processing failed.", ct);
        }
    }

    internal static TimeSpan DelayAfterFailedAttempt(int attemptCount)
    {
        if (attemptCount <= 0 || attemptCount >= OnlineOrderIntegrationStatuses.MaxAttempts)
            return TimeSpan.Zero;

        return TimeSpan.FromMinutes(1 << (attemptCount - 1));
    }

    private async Task ProcessCoreAsync(AppDbContext db, OnlineOrderIntegration row, CancellationToken ct)
    {
        OnlineOrderIntegrationWebhookBody? body;
        try
        {
            body = JsonSerializer.Deserialize<OnlineOrderIntegrationWebhookBody>(row.RawPayload ?? "{}", JsonOptions);
        }
        catch (JsonException)
        {
            await MarkFailedAsync(db, row, "Payload could not be read.", ct);
            return;
        }

        if (body is null)
        {
            await MarkFailedAsync(db, row, "Payload could not be read.", ct);
            return;
        }

        var location = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.TenantId == row.TenantId
                     && l.Provider == row.Provider
                     && l.ExternalLocationId == row.ExternalLocationId
                     && l.IsActive
                     && l.Enabled,
                ct);
        if (location is null)
        {
            await MarkFailedAsync(db, row, "Location is not enabled.", ct);
            return;
        }

        if (!await TryBuildLinesAsync(db, row, location, body, ct))
            return;

        if (string.IsNullOrWhiteSpace(location.ClaimingUserId))
        {
            var now = _time.GetUtcNow().UtcDateTime;
            row.Status = OnlineOrderIntegrationStatuses.Mapped;
            row.NextAttemptAt = null;
            row.ProcessedAt = now;
            row.UpdatedAt = now;
            row.LastError = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        await _audit.LogSystemOperationAsync(
            action: "online_order_integration_cart_push",
            entityType: "online_order_integration",
            userId: "worker",
            userRole: "worker",
            description: "Automatic cart bridge attempt",
            tenantId: row.TenantId,
            entityId: row.Id,
            newValues: new
            {
                actor = "worker",
                tenant_id = row.TenantId,
                integration_row_id = row.Id,
                claiming_user_id = location.ClaimingUserId,
                timestamp = _time.GetUtcNow().UtcDateTime,
                outcome = "push_attempted",
            });

        var push = await _carts.PushOrderToPosAsync(row.OnlineOrderId!.Value, location.ClaimingUserId, ct);
        var finished = _time.GetUtcNow().UtcDateTime;
        if (!push.Succeeded)
        {
            await ScheduleRetryAsync(db, row, push.Error ?? push.Code ?? "Cart push failed.", ct);
            return;
        }

        row.Status = OnlineOrderIntegrationStatuses.CartLinked;
        row.NextAttemptAt = null;
        row.LastError = null;
        row.ProcessedAt = finished;
        row.UpdatedAt = finished;
        await db.SaveChangesAsync(ct);

        try
        {
            await _audit.LogSystemOperationAsync(
                action: "online_order_integration_cart_push",
                entityType: "online_order_integration",
                userId: "worker",
                userRole: "worker",
                description: "Automatic cart bridge finished",
                tenantId: row.TenantId,
                entityId: row.Id,
                newValues: new
                {
                    actor = "worker",
                    tenant_id = row.TenantId,
                    integration_row_id = row.Id,
                    claiming_user_id = location.ClaimingUserId,
                    timestamp = finished,
                    outcome = push.AlreadyPushed ? "already_pushed" : "cart_linked",
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Follow-up audit failed for integration {IntegrationId}", row.Id);
        }
    }

    private async Task<bool> TryBuildLinesAsync(
        AppDbContext db,
        OnlineOrderIntegration row,
        OnlineOrderIntegrationLocation location,
        OnlineOrderIntegrationWebhookBody body,
        CancellationToken ct)
    {
        var items = body.Items ?? new List<OnlineOrderIntegrationWebhookItem>();
        if (items.Count == 0 || items.Any(i => i.ProductId == Guid.Empty || i.Quantity < 1))
        {
            await MarkFailedAsync(db, row, "Payload items are invalid.", ct);
            return false;
        }

        var delivery = Money(body.DeliveryFee);
        var service = Money(body.ServiceFee);
        if (delivery < 0 || service < 0)
        {
            await MarkFailedAsync(db, row, "Fees cannot be negative.", ct);
            return false;
        }

        if (delivery > 0 && location.DeliveryFeeProductId is null)
        {
            await MarkFailedAsync(db, row, "Delivery fee is present and delivery_fee_product_id is missing.", ct);
            return false;
        }

        if (service > 0 && location.ServiceFeeProductId is null)
        {
            await MarkFailedAsync(db, row, "Service fee is present and service_fee_product_id is missing.", ct);
            return false;
        }

        var productIds = items.Select(i => i.ProductId).ToList();
        if (delivery > 0)
            productIds.Add(location.DeliveryFeeProductId!.Value);
        if (service > 0)
            productIds.Add(location.ServiceFeeProductId!.Value);

        var products = await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == row.TenantId && p.IsActive && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
        var known = products.Select(p => p.Id).ToHashSet();
        if (productIds.Any(id => !known.Contains(id)))
        {
            await MarkFailedAsync(db, row, "One or more products are not in the tenant catalog.", ct);
            return false;
        }

        var names = products.ToDictionary(p => p.Id, p => p.Name);
        decimal sum = 0m;
        var lines = new List<(Guid ProductId, string Name, int Quantity, decimal UnitPrice, decimal Total)>();
        foreach (var item in items)
        {
            var unit = Money(item.UnitPrice);
            var lineTotal = Money(unit * item.Quantity);
            sum += lineTotal;
            var name = string.IsNullOrWhiteSpace(item.Name) ? names[item.ProductId] : item.Name.Trim();
            lines.Add((item.ProductId, name, item.Quantity, unit, lineTotal));
        }

        if (delivery > 0)
        {
            sum += delivery;
            lines.Add((location.DeliveryFeeProductId!.Value, names[location.DeliveryFeeProductId.Value], 1, delivery, delivery));
        }

        if (service > 0)
        {
            sum += service;
            lines.Add((location.ServiceFeeProductId!.Value, names[location.ServiceFeeProductId.Value], 1, service, service));
        }

        sum = Money(sum);
        if (sum != Money(body.ChannelTotal))
        {
            await MarkFailedAsync(
                db,
                row,
                string.Create(CultureInfo.InvariantCulture, $"Receipt total {sum:0.00} does not equal channel total {Money(body.ChannelTotal):0.00}."),
                ct);
            return false;
        }

        var nameOk = (body.CustomerName ?? string.Empty).Trim();
        var phone = (body.CustomerPhone ?? string.Empty).Trim();
        if (nameOk.Length < 2 || phone.Count(char.IsDigit) < 6)
        {
            await MarkFailedAsync(db, row, "Customer name or phone is invalid.", ct);
            return false;
        }

        if (row.OnlineOrderId is null)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var order = new OnlineOrder
            {
                Id = Guid.NewGuid(),
                TenantId = row.TenantId,
                OrderNumber = await NextOrderNumberAsync(db, row.TenantId, ct),
                CustomerName = nameOk,
                CustomerPhone = phone,
                CustomerEmail = string.IsNullOrWhiteSpace(body.CustomerEmail) ? null : body.CustomerEmail.Trim(),
                OrderType = NormalizeOrderType(body.OrderType),
                DeliveryAddress = string.IsNullOrWhiteSpace(body.DeliveryAddress) ? null : body.DeliveryAddress.Trim(),
                Notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
                PaymentMethod = OnlineOrderPaymentMethods.Online,
                PaymentStatus = OnlineOrderPaymentStatuses.Pending,
                OrderStatus = OnlineOrderStatuses.Pending,
                Source = OnlineOrderSources.Marketplace,
                Subtotal = sum,
                Tax = 0m,
                Total = sum,
                CreatedAt = now,
            };
            foreach (var line in lines)
            {
                order.Items.Add(new OnlineOrderItem
                {
                    Id = Guid.NewGuid(),
                    OnlineOrderId = order.Id,
                    ProductId = line.ProductId,
                    ProductName = line.Name.Length > 200 ? line.Name[..200] : line.Name,
                    Quantity = line.Quantity,
                    Price = line.UnitPrice,
                    Total = line.Total,
                });
            }

            db.OnlineOrders.Add(order);
            db.OnlineOrderStatusChanges.Add(new OnlineOrderStatusChange
            {
                Id = Guid.NewGuid(),
                OnlineOrderId = order.Id,
                TenantId = row.TenantId,
                FromStatus = string.Empty,
                ToStatus = OnlineOrderStatuses.Pending,
                ChangedAt = now,
                Reason = "Marketplace integration",
            });
            row.OnlineOrderId = order.Id;
            row.Status = OnlineOrderIntegrationStatuses.Mapped;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    private async Task ScheduleRetryAsync(
        AppDbContext db,
        OnlineOrderIntegration row,
        string message,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        row.AttemptCount += 1;
        row.ProcessedAt = now;
        row.UpdatedAt = now;
        row.LastError = TrimError(message);
        if (row.AttemptCount >= OnlineOrderIntegrationStatuses.MaxAttempts)
        {
            row.Status = OnlineOrderIntegrationStatuses.Failed;
            row.NextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            await NotifyFailedAsync(row, ct);
            return;
        }

        row.NextAttemptAt = now.Add(DelayAfterFailedAttempt(row.AttemptCount));
        await db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(
        AppDbContext db,
        OnlineOrderIntegration row,
        string message,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        row.Status = OnlineOrderIntegrationStatuses.Failed;
        row.NextAttemptAt = null;
        row.ProcessedAt = now;
        row.UpdatedAt = now;
        row.LastError = TrimError(message);
        if (row.AttemptCount < 1)
            row.AttemptCount = 1;
        await db.SaveChangesAsync(ct);
        await NotifyFailedAsync(row, ct);
    }

    private async Task NotifyFailedAsync(OnlineOrderIntegration row, CancellationToken ct)
    {
        try
        {
            await _activity.TryPublishAsync(
                row.TenantId,
                ActivityEventType.OnlineOrderIntegrationFailed,
                new
                {
                    IntegrationRowId = row.Id.ToString("D"),
                    ExternalOrderId = row.ExternalOrderId,
                    Outcome = OnlineOrderIntegrationStatuses.Failed,
                    Reason = row.LastError,
                },
                actorUserId: "worker",
                dedupKey: $"online_order_integration_failed_{row.Id:N}",
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operator notification failed for integration {IntegrationId}", row.Id);
        }
    }

    private static async Task<string> NextOrderNumberAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var count = await db.OnlineOrders.IgnoreQueryFilters().CountAsync(o => o.TenantId == tenantId, ct);
        return string.Create(CultureInfo.InvariantCulture, $"ORD-{(count + 1):D3}");
    }

    private static string NormalizeOrderType(string? value)
    {
        var v = (value ?? OnlineOrderTypes.Delivery).Trim().ToLowerInvariant();
        return OnlineOrderTypes.All.Contains(v) ? v : OnlineOrderTypes.Delivery;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string TrimError(string message) =>
        message.Length <= 500 ? message : message[..500];

    internal static string PayloadSha256(string raw)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
