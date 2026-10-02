using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Imei;

public sealed class ProductImeiService : IProductImeiService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public ProductImeiService(AppDbContext db, ICurrentTenantAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<IReadOnlyList<ProductImeiDto>?> ListAsync(
        Guid productId,
        ProductImeiStatus? status,
        CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(productId, cancellationToken).ConfigureAwait(false))
            return null;

        var query = _db.ProductImeis.AsNoTracking().Where(row => row.ProductId == productId);
        if (status.HasValue)
            query = query.Where(row => row.Status == status.Value);

        var rows = await query
            .OrderBy(row => row.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.ConvertAll(ToDto);
    }

    public async Task<ProductImeiAddResult> AddAsync(
        Guid productId,
        AddProductImeiRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return new ProductImeiAddResult.NotFound();

        var product = await _db.Products
            .FirstOrDefaultAsync(row => row.Id == productId, cancellationToken)
            .ConfigureAwait(false);
        if (product == null)
            return new ProductImeiAddResult.NotFound();

        if (!product.ImeiTracked)
        {
            return new ProductImeiAddResult.Error(
                ProductImeiErrorCodes.TrackingDisabled,
                "IMEI tracking is not enabled for this product.",
                StatusCodes.Status400BadRequest);
        }

        var imei = NormalizeImei(request.Imei);
        if (imei == null)
        {
            return new ProductImeiAddResult.Error(
                ProductImeiErrorCodes.Required,
                "IMEI is required.",
                StatusCodes.Status400BadRequest);
        }

        var duplicate = await _db.ProductImeis
            .AnyAsync(row => row.Imei == imei, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            return new ProductImeiAddResult.Error(
                ProductImeiErrorCodes.Duplicate,
                "IMEI already exists for this tenant.",
                StatusCodes.Status409Conflict);
        }

        var row = new ProductImei
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Imei = imei,
            Status = ProductImeiStatus.InStock,
            WarrantyMonths = request.WarrantyMonths,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.ProductImeis.Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProductImeiAddResult.Ok(ToDto(row));
    }

    public static string? NormalizeImei(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length is < 8 or > 20 ? null : trimmed;
    }

    public static IReadOnlyList<string> ResolveRequestedImeis(string? imei, IReadOnlyList<string>? imeis)
    {
        var list = new List<string>();
        if (imeis != null)
        {
            foreach (var item in imeis)
            {
                var normalized = NormalizeImei(item);
                if (normalized != null)
                    list.Add(normalized);
            }
        }

        var single = NormalizeImei(imei);
        if (single != null)
            list.Add(single);
        return list;
    }

    private async Task<bool> ProductExistsAsync(Guid productId, CancellationToken cancellationToken) =>
        await _db.Products.AsNoTracking().AnyAsync(row => row.Id == productId, cancellationToken)
            .ConfigureAwait(false);

    private static ProductImeiDto ToDto(ProductImei row) => new()
    {
        Id = row.Id,
        ProductId = row.ProductId,
        Imei = row.Imei,
        Status = row.Status,
        SoldPaymentId = row.SoldPaymentId,
        WarrantyMonths = row.WarrantyMonths,
        CreatedAtUtc = row.CreatedAtUtc,
        SoldAtUtc = row.SoldAtUtc,
    };
}
