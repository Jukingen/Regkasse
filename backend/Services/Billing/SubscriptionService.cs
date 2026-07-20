using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Billing;

/// <summary>
/// Digital-service subscription lifecycle (create / cancel / list).
/// Prices come from <see cref="ServicePricingData"/>; no payment capture on create.
/// Uses the request-scoped <see cref="AppDbContext"/> (not <see cref="IDbContextFactory{TContext}"/>)
/// so <see cref="KasseAPI_Final.Tenancy.ICurrentTenantAccessor"/> resolves correctly.
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    public const string ServiceNotFoundCode = "SERVICE_NOT_FOUND";
    public const string TenantNotFoundCode = "TENANT_NOT_FOUND";
    public const string AlreadyActiveCode = "SUBSCRIPTION_ALREADY_ACTIVE";
    public const string NotFoundCode = "SUBSCRIPTION_NOT_FOUND";
    public const string AlreadyCancelledCode = "SUBSCRIPTION_ALREADY_CANCELLED";

    private readonly AppDbContext _db;
    private readonly IBillingAuditService _audit;
    private readonly TimeProvider _time;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        AppDbContext db,
        IBillingAuditService audit,
        TimeProvider time,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _audit = audit;
        _time = time;
        _logger = logger;
    }

    public async Task<SubscriptionResult> CreateSubscriptionAsync(
        Guid tenantId,
        string serviceId,
        string? actorUserId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return SubscriptionResult.Fail(ServiceNotFoundCode, "Service not found");

        var pricing = ServicePricingData.GetByServiceId(serviceId);
        if (pricing is null)
            return SubscriptionResult.Fail(ServiceNotFoundCode, "Service not found");

        var db = _db;

        var tenantExists = await db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);
        if (!tenantExists)
            return SubscriptionResult.Fail(TenantNotFoundCode, "Tenant not found.");

        var hasActive = await db.Subscriptions.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                s => s.TenantId == tenantId
                     && s.ServiceId == pricing.ServiceId
                     && s.Status == SubscriptionStatuses.Active,
                ct);
        if (hasActive)
            return SubscriptionResult.Fail(AlreadyActiveCode, "An active subscription already exists for this service.");

        var now = _time.GetUtcNow().UtcDateTime;
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = pricing.ServiceId,
            Price = pricing.PriceMonthly,
            Currency = pricing.Currency,
            Status = SubscriptionStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now,
            NextBillingDate = now.AddMonths(1),
            CreatedByUserId = actorUserId
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Digital subscription {SubscriptionId} created for tenant {TenantId} service {ServiceId}",
            subscription.Id,
            tenantId,
            pricing.ServiceId);

        if (Guid.TryParse(actorUserId, out var actorGuid) && actorGuid != Guid.Empty)
        {
            await _audit.LogAsync(
                BillingAuditEventTypes.SubscriptionCreated,
                actorGuid,
                tenantId,
                saleId: null,
                details: JsonSerializer.Serialize(new
                {
                    subscriptionId = subscription.Id,
                    serviceId = subscription.ServiceId,
                    price = subscription.Price,
                    currency = subscription.Currency
                }),
                ct: ct);
        }

        return SubscriptionResult.Success(subscription);
    }

    public async Task<SubscriptionResult> CancelSubscriptionAsync(
        Guid subscriptionId,
        string? actorUserId = null,
        CancellationToken ct = default)
    {
        var db = _db;
        var subscription = await db.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
        if (subscription is null)
            return SubscriptionResult.Fail(NotFoundCode, "Subscription not found.");

        if (subscription.Status == SubscriptionStatuses.Cancelled)
            return SubscriptionResult.Fail(AlreadyCancelledCode, "Subscription is already cancelled.");

        var now = _time.GetUtcNow().UtcDateTime;
        subscription.Status = SubscriptionStatuses.Cancelled;
        subscription.CancelledAtUtc = now;
        subscription.CancelledByUserId = actorUserId;
        subscription.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (Guid.TryParse(actorUserId, out var actorGuid) && actorGuid != Guid.Empty)
        {
            await _audit.LogAsync(
                BillingAuditEventTypes.SubscriptionCancelled,
                actorGuid,
                subscription.TenantId,
                saleId: null,
                details: JsonSerializer.Serialize(new
                {
                    subscriptionId = subscription.Id,
                    serviceId = subscription.ServiceId
                }),
                ct: ct);
        }

        return SubscriptionResult.Success(subscription);
    }

    public async Task<IReadOnlyList<Subscription>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var db = _db;
        return await db.Subscriptions.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Subscription?> GetByIdAsync(
        Guid subscriptionId,
        CancellationToken ct = default)
    {
        var db = _db;
        return await db.Subscriptions.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
    }

    public async Task<DigitalBillingDashboardDto> GetDigitalBillingDashboardAsync(
        CancellationToken ct = default)
    {
        var db = _db;

        var rows = await (
            from s in db.Subscriptions.AsNoTracking().IgnoreQueryFilters()
            join t in db.Tenants.AsNoTracking().IgnoreQueryFilters() on s.TenantId equals t.Id
            orderby s.CreatedAt descending
            select new { Subscription = s, TenantName = t.Name }
        ).ToListAsync(ct);

        decimal websites = 0;
        decimal apps = 0;
        var subscribers = 0;

        var mapped = new List<DigitalBillingSubscriptionRowDto>(rows.Count);
        foreach (var row in rows)
        {
            var sub = row.Subscription;
            var catalog = ServicePricingData.GetByServiceId(sub.ServiceId);
            var serviceName = catalog?.Name ?? sub.ServiceId;
            var type = catalog?.Type
                       ?? (sub.ServiceId.StartsWith("app-", StringComparison.OrdinalIgnoreCase)
                           ? ServicePricingTypes.App
                           : ServicePricingTypes.Website);

            if (sub.Status == SubscriptionStatuses.Active)
            {
                subscribers++;
                if (string.Equals(type, ServicePricingTypes.App, StringComparison.OrdinalIgnoreCase))
                    apps += sub.Price;
                else
                    websites += sub.Price;
            }

            mapped.Add(new DigitalBillingSubscriptionRowDto
            {
                Id = sub.Id,
                TenantId = sub.TenantId,
                Tenant = row.TenantName,
                Service = serviceName,
                ServiceId = sub.ServiceId,
                Price = sub.Price,
                Currency = sub.Currency,
                StartDate = sub.CreatedAt,
                NextBilling = sub.NextBillingDate,
                Status = sub.Status
            });
        }

        return new DigitalBillingDashboardDto
        {
            Total = websites + apps,
            Websites = websites,
            Apps = apps,
            Subscribers = subscribers,
            Currency = "EUR",
            Subscriptions = mapped
        };
    }
}
