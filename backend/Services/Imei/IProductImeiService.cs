using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Imei;

public interface IProductImeiService
{
    Task<IReadOnlyList<ProductImeiDto>?> ListAsync(
        Guid productId,
        ProductImeiStatus? status,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tenant-wide IMEI list. <paramref name="productId"/> is optional.
    /// Returns null when a product id is set and that product does not exist.
    /// </summary>
    Task<IReadOnlyList<AdminProductImeiListItemDto>?> SearchAsync(
        Guid? productId,
        ProductImeiStatus? status,
        CancellationToken cancellationToken);

    Task<ProductImeiAddResult> AddAsync(
        Guid productId,
        AddProductImeiRequest request,
        CancellationToken cancellationToken);
}

public abstract record ProductImeiAddResult
{
    public sealed record Ok(ProductImeiDto Item) : ProductImeiAddResult;
    public sealed record NotFound() : ProductImeiAddResult;
    public sealed record Error(string Code, string Message, int StatusCode) : ProductImeiAddResult;
}
