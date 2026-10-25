using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Products;

public interface IProductVariantService
{
    Task<IReadOnlyList<ProductVariantDto>?> ListAsync(Guid productId, CancellationToken cancellationToken);

    Task<ProductVariantMutation> CreateAsync(
        Guid productId,
        CreateProductVariantRequest request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<ProductVariantMutation> UpdateAsync(
        Guid productId,
        Guid variantId,
        UpdateProductVariantRequest request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<ProductVariantMutation> DeactivateAsync(
        Guid productId,
        Guid variantId,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<ProductVariantMutation> AdjustStockAsync(
        Guid productId,
        Guid variantId,
        int delta,
        string actorUserId,
        CancellationToken cancellationToken);
}
