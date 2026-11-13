using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.AdminProducts;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class DemoProductImportImageService : IDemoProductImportImageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IOptions<ProductMediaOptions> _mediaOptions;
    private readonly ProductImageThumbnailService _thumbnailService;
    private readonly ILogger<DemoProductImportImageService> _logger;

    public DemoProductImportImageService(
        IWebHostEnvironment environment,
        IOptions<ProductMediaOptions> mediaOptions,
        ProductImageThumbnailService thumbnailService,
        ILogger<DemoProductImportImageService> logger)
    {
        _environment = environment;
        _mediaOptions = mediaOptions;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    public async Task TryAssignPlaceholderAsync(
        Guid tenantId,
        Product product,
        string categoryName,
        DemoImportImageMode mode,
        CancellationToken cancellationToken = default)
    {
        if (mode == DemoImportImageMode.None)
            return;

        try
        {
            var sourcePng = mode switch
            {
                DemoImportImageMode.DefaultAsset => DemoProductPlaceholderImageGenerator.CreateDefaultFoodPng(),
                _ => DemoProductPlaceholderImageGenerator.CreateCategoryPlaceholderPng(
                    categoryName,
                    product.Name),
            };

            var webp = await _thumbnailService
                .CreateSquareThumbnailWebpAsync(sourcePng, cancellationToken)
                .ConfigureAwait(false);

            var opts = _mediaOptions.Value;
            var root = Path.Combine(_environment.ContentRootPath, opts.RootRelativeDirectory);
            var productDir = Path.Combine(root, tenantId.ToString("D"), product.Id.ToString("D"));
            Directory.CreateDirectory(productDir);

            foreach (var existing in Directory.Exists(productDir) ? Directory.GetFiles(productDir) : [])
            {
                try
                {
                    File.Delete(existing);
                }
                catch
                {
                    // best-effort
                }
            }

            var fileName = $"demo-{Guid.NewGuid():N}.webp";
            var absolutePath = Path.Combine(productDir, fileName);
            await File.WriteAllBytesAsync(absolutePath, webp, cancellationToken).ConfigureAwait(false);

            product.ImageUrl = BuildPublicUrl(opts, tenantId, product.Id, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Demo import placeholder image failed for product {ProductId} ({ProductName})",
                product.Id,
                product.Name);
        }
    }

    private static string BuildPublicUrl(ProductMediaOptions opts, Guid tenantId, Guid productId, string fileName)
    {
        var prefix = opts.PublicUrlPathPrefix.TrimEnd('/');
        if (!prefix.StartsWith('/'))
            prefix = "/" + prefix;

        var baseUrl = string.IsNullOrWhiteSpace(opts.PublicBaseUrl)
            ? "http://localhost:5184"
            : opts.PublicBaseUrl.Trim().TrimEnd('/');

        return $"{baseUrl}{prefix}/{tenantId:D}/{productId:D}/{fileName}";
    }
}
