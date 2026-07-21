using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace KasseAPI_Final.Services.AdminProducts;

/// <summary>
/// Thrown when uploaded bytes are not a decodable image or processing fails. Maps to HTTP 400 for uploads.
/// </summary>
public sealed class ProductImageProcessingException : Exception
{
    public ProductImageProcessingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Builds a square WebP thumbnail using center-crop (no distortion). Uses SixLabors.ImageSharp — Apache-2.0, widely used on .NET, supports decode + resize + WebP encode without native dependencies.
/// </summary>
public sealed class ProductImageThumbnailService
{
    private readonly IOptions<ProductMediaOptions> _options;
    private readonly ILogger<ProductImageThumbnailService> _logger;

    public ProductImageThumbnailService(
        IOptions<ProductMediaOptions> options,
        ILogger<ProductImageThumbnailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Decode image bytes, center-crop to a square of <see cref="ProductMediaOptions.ThumbnailEdgePixels"/>, encode WebP. Original pixels are not retained on disk (caller writes only the returned bytes).
    /// </summary>
    public async Task<byte[]> CreateSquareThumbnailWebpAsync(ReadOnlyMemory<byte> sourceBytes, CancellationToken cancellationToken)
    {
        var edge = Math.Clamp(_options.Value.ThumbnailEdgePixels, 32, 2048);
        var quality = Math.Clamp(_options.Value.WebpEncodingQuality, 1, 100);

        try
        {
            await using var input = new MemoryStream(sourceBytes.ToArray(), writable: false);
            using var image = await Image.LoadAsync(input, cancellationToken);

            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(edge, edge),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center
            }));

            await using var output = new MemoryStream();
            await image.SaveAsync(output, new WebpEncoder { Quality = quality }, cancellationToken);
            return output.ToArray();
        }
        catch (UnknownImageFormatException ex)
        {
            _logger.LogWarning(ex, "Product image upload rejected: unknown image format");
            throw new ProductImageProcessingException("Invalid or corrupt image (could not decode).", ex);
        }
        catch (InvalidImageContentException ex)
        {
            _logger.LogWarning(ex, "Product image upload rejected: invalid image content");
            throw new ProductImageProcessingException("Invalid or corrupt image content.", ex);
        }
        catch (ImageProcessingException ex)
        {
            _logger.LogWarning(ex, "Product image processing failed");
            throw new ProductImageProcessingException("Image could not be processed.", ex);
        }
    }
}
