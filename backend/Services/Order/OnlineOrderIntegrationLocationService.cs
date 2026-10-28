using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntegrationLocationService
{
    Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderIntegrationLocationRequest request,
        CancellationToken ct = default);
}

public sealed class OnlineOrderIntegrationLocationService : IOnlineOrderIntegrationLocationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _time;

    public OnlineOrderIntegrationLocationService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        IHostEnvironment environment,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _environment = environment;
        _time = time;
    }

    public async Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderIntegrationLocationRequest request,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        var provider = request.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        var locationId = request.ExternalLocationId?.Trim() ?? string.Empty;
        if (provider.Length is < 1 or > 32 || locationId.Length is < 1 or > 128)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider and location id are required.");

        if (request.Enabled
            && _environment.IsProduction()
            && !OnlineOrderIntegrationSignatureStatus.DeliverectConfirmed)
        {
            return OnlineOrderIntegrationCommandResult.Fail(
                StatusCodes.Status409Conflict,
                OnlineOrderIntegrationSignatureStatus.UnconfirmedCode,
                "Location cannot be enabled while the Deliverect signature scheme is unconfirmed.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var tenantExists = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);
        if (!tenantExists)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        if (!string.IsNullOrWhiteSpace(request.ClaimingUserId))
        {
            var userId = request.ClaimingUserId.Trim();
            var userExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
            if (!userExists)
                return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "CLAIMING_USER_NOT_FOUND", "Claiming user was not found.");
        }

        if (request.DeliveryFeeProductId is Guid deliveryId
            && !await ProductInTenantAsync(db, tenantId, deliveryId, ct))
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "PRODUCTS_INVALID", "Delivery fee product was not found.");

        if (request.ServiceFeeProductId is Guid serviceId
            && !await ProductInTenantAsync(db, tenantId, serviceId, ct))
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "PRODUCTS_INVALID", "Service fee product was not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        var row = await db.OnlineOrderIntegrationLocations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                l => l.Provider == provider && l.ExternalLocationId == locationId,
                ct);
        if (row is not null && row.TenantId != tenantId)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "LOCATION_NOT_FOUND", "Location was not found.");

        if (row is null)
        {
            row = new OnlineOrderIntegrationLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = provider,
                ExternalLocationId = locationId,
                CreatedAt = now,
                IsActive = true,
            };
            db.OnlineOrderIntegrationLocations.Add(row);
        }

        row.ClaimingUserId = string.IsNullOrWhiteSpace(request.ClaimingUserId) ? null : request.ClaimingUserId.Trim();
        row.Enabled = request.Enabled;
        row.DeliveryFeeProductId = request.DeliveryFeeProductId;
        row.ServiceFeeProductId = request.ServiceFeeProductId;
        row.UpdatedAt = now;
        row.IsActive = true;
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    private static Task<bool> ProductInTenantAsync(AppDbContext db, Guid tenantId, Guid productId, CancellationToken ct) =>
        db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == productId && p.TenantId == tenantId && p.IsActive, ct);
}
