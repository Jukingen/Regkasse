using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;
using SkiaSharp;

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
/// Builds a square WebP thumbnail using center-crop (no distortion). Uses SkiaSharp (MIT).
/// </summary>
public sealed class ProductImageThumbnailService
{
    private static readonly SKSamplingOptions HighQualitySampling = new(SKCubicResampler.Mitchell);

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
    public Task<byte[]> CreateSquareThumbnailWebpAsync(ReadOnlyMemory<byte> sourceBytes, CancellationToken cancellationToken)
    {
        var edge = Math.Clamp(_options.Value.ThumbnailEdgePixels, 32, 2048);
        var quality = Math.Clamp(_options.Value.WebpEncodingQuality, 1, 100);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = SKBitmap.Decode(sourceBytes.Span);
            if (bitmap is null)
                throw new ProductImageProcessingException("Invalid or corrupt image (could not decode).");

            using var cropped = CenterCropSquare(bitmap, edge);
            using var image = SKImage.FromBitmap(cropped);
            using var encoded = image.Encode(SKEncodedImageFormat.Webp, quality)
                ?? throw new ProductImageProcessingException("Image could not be processed.");
            return Task.FromResult(encoded.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProductImageProcessingException ex)
        {
            _logger.LogWarning(ex, "Product image upload rejected");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Product image processing failed");
            throw new ProductImageProcessingException("Image could not be processed.", ex);
        }
    }

    private static SKBitmap CenterCropSquare(SKBitmap source, int edge)
    {
        if (source.Width <= 0 || source.Height <= 0)
            throw new ProductImageProcessingException("Invalid or corrupt image content.");

        var scale = Math.Max(edge / (double)source.Width, edge / (double)source.Height);
        var scaledWidth = Math.Max(edge, (int)Math.Ceiling(source.Width * scale));
        var scaledHeight = Math.Max(edge, (int)Math.Ceiling(source.Height * scale));
        var scaledInfo = new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var scaled = source.Resize(scaledInfo, HighQualitySampling)
            ?? throw new ProductImageProcessingException("Image could not be processed.");

        var cropX = Math.Max(0, (scaled.Width - edge) / 2);
        var cropY = Math.Max(0, (scaled.Height - edge) / 2);
        var output = new SKBitmap(new SKImageInfo(edge, edge, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.Transparent);
        var sourceRect = SKRect.Create(cropX, cropY, edge, edge);
        canvas.DrawBitmap(scaled, sourceRect, SKRect.Create(edge, edge));
        canvas.Flush();
        return output;
    }
}
