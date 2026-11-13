using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.AdminProducts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Xunit;

namespace KasseAPI_Final.Tests.Services.AdminProducts;

public class ProductImageThumbnailServiceTests
{
    [Fact]
    public async Task CreateSquareThumbnailWebpAsync_ProducesConfiguredSquareWebp()
    {
        var pngBytes = CreateSolidPng(240, 100, new SKColor(255, 0, 0));

        var svc = new ProductImageThumbnailService(
            Options.Create(new ProductMediaOptions { ThumbnailEdgePixels = 120, WebpEncodingQuality = 82 }),
            NullLogger<ProductImageThumbnailService>.Instance);

        var webp = await svc.CreateSquareThumbnailWebpAsync(pngBytes, CancellationToken.None);

        Assert.NotEmpty(webp);
        Assert.True(webp.Length > 12);
        Assert.Equal("RIFF"u8.ToArray(), webp.AsSpan(0, 4).ToArray());
        Assert.Equal("WEBP"u8.ToArray(), webp.AsSpan(8, 4).ToArray());

        using var result = SKBitmap.Decode(webp);
        Assert.NotNull(result);
        Assert.Equal(120, result.Width);
        Assert.Equal(120, result.Height);
    }

    [Fact]
    public async Task CreateSquareThumbnailWebpAsync_ThrowsProductImageProcessing_OnGarbage()
    {
        var svc = new ProductImageThumbnailService(
            Options.Create(new ProductMediaOptions()),
            NullLogger<ProductImageThumbnailService>.Instance);

        await Assert.ThrowsAsync<ProductImageProcessingException>(() =>
            svc.CreateSquareThumbnailWebpAsync(new byte[] { 1, 2, 3 }, CancellationToken.None));
    }

    private static byte[] CreateSolidPng(int width, int height, SKColor color)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(color);

        using var snapshot = SKImage.FromBitmap(bitmap);
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }
}
