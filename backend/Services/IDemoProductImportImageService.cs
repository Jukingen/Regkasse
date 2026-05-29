using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IDemoProductImportImageService
{
    /// <summary>
    /// Writes a WebP thumbnail under product media storage and sets <see cref="Product.ImageUrl"/>.
    /// No-op when mode is <see cref="DemoImportImageMode.None"/>.
    /// </summary>
    Task TryAssignPlaceholderAsync(
        Guid tenantId,
        Product product,
        string categoryName,
        DemoImportImageMode mode,
        CancellationToken cancellationToken = default);
}
