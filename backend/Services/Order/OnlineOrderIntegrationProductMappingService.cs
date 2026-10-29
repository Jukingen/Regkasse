using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderIntegrationProductMappingService
{
    Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderProductMappingRequest request,
        CancellationToken ct = default);

    Task<OnlineOrderProductMappingListOutcome> ListAsync(
        string? provider,
        CancellationToken ct = default);
}

public sealed class OnlineOrderProductMappingListOutcome
{
    public bool Succeeded { get; init; }

    public int StatusCode { get; init; }

    public string? Code { get; init; }

    public string? Error { get; init; }

    public OnlineOrderProductMappingListDto? Body { get; init; }

    public static OnlineOrderProductMappingListOutcome Ok(OnlineOrderProductMappingListDto body) =>
        new() { Succeeded = true, StatusCode = StatusCodes.Status200OK, Body = body };

    public static OnlineOrderProductMappingListOutcome Fail(int statusCode, string code, string error) =>
        new() { Succeeded = false, StatusCode = statusCode, Code = code, Error = error };
}

public sealed class OnlineOrderIntegrationProductMappingService : IOnlineOrderIntegrationProductMappingService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly TimeProvider _time;

    public OnlineOrderIntegrationProductMappingService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        TimeProvider time)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _time = time;
    }

    public async Task<OnlineOrderIntegrationCommandResult> UpsertAsync(
        UpsertOnlineOrderProductMappingRequest request,
        CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        var provider = request.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        var externalId = request.ExternalProductId?.Trim() ?? string.Empty;
        if (provider.Length is < 1 or > 32 || externalId.Length is < 1 or > 128 || request.ProductId == Guid.Empty)
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider, external product id, and product are required.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (!await TenantExistsAsync(db, tenantId, ct))
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        if (!await ProductInTenantAsync(db, tenantId, request.ProductId, ct))
            return OnlineOrderIntegrationCommandResult.Fail(StatusCodes.Status400BadRequest, "PRODUCTS_INVALID", "Product was not found.");

        var now = _time.GetUtcNow().UtcDateTime;
        var row = await db.OnlineOrderProductMappings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                m => m.TenantId == tenantId && m.Provider == provider && m.ExternalProductId == externalId,
                ct);
        if (row is null)
        {
            row = new OnlineOrderProductMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Provider = provider,
                ExternalProductId = externalId,
                CreatedAt = now,
            };
            db.OnlineOrderProductMappings.Add(row);
        }

        row.ProductId = request.ProductId;
        row.UpdatedAt = now;
        row.IsActive = true;
        await db.SaveChangesAsync(ct);
        return OnlineOrderIntegrationCommandResult.Ok(StatusCodes.Status200OK, row.Id);
    }

    public async Task<OnlineOrderProductMappingListOutcome> ListAsync(string? provider, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return OnlineOrderProductMappingListOutcome.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        var normalized = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim().ToLowerInvariant();
        if (normalized is { Length: > 32 })
            return OnlineOrderProductMappingListOutcome.Fail(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Provider is invalid.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (!await TenantExistsAsync(db, tenantId, ct))
            return OnlineOrderProductMappingListOutcome.Fail(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND", "Tenant was not found.");

        var query = db.OnlineOrderProductMappings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && m.IsActive);
        if (normalized is not null)
            query = query.Where(m => m.Provider == normalized);

        var rows = await query
            .OrderBy(m => m.Provider)
            .ThenBy(m => m.ExternalProductId)
            .Select(m => new { m.Id, m.Provider, m.ExternalProductId, m.ProductId, m.CreatedAt, m.UpdatedAt })
            .ToListAsync(ct);

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var names = await db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return OnlineOrderProductMappingListOutcome.Ok(new OnlineOrderProductMappingListDto
        {
            Items = rows.Select(r => new OnlineOrderProductMappingDto
            {
                Id = r.Id,
                Provider = r.Provider,
                ExternalProductId = r.ExternalProductId,
                ProductId = r.ProductId,
                ProductName = names.TryGetValue(r.ProductId, out var name) ? name : string.Empty,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            }).ToList(),
        });
    }

    private static Task<bool> TenantExistsAsync(AppDbContext db, Guid tenantId, CancellationToken ct) =>
        db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && t.IsActive && t.DeletedAtUtc == null, ct);

    private static Task<bool> ProductInTenantAsync(AppDbContext db, Guid tenantId, Guid productId, CancellationToken ct) =>
        db.Products.AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == productId && p.TenantId == tenantId && p.IsActive, ct);
}
