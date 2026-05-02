using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.AdminProducts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace KasseAPI_Final.Tests.Services.AdminProducts;

public class ProductImageThumbnailServiceTests
{
    [Fact]
    public async Task CreateSquareThumbnailWebpAsync_ProducesConfiguredSquareWebp()
    {
        byte[] pngBytes;
        using (var img = new Image<Rgba32>(240, 100))
        {
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                        row[x] = new Rgba32(255, 0, 0, 255);
                }
            });
            await using var ms = new MemoryStream();
            await img.SaveAsPngAsync(ms);
            pngBytes = ms.ToArray();
        }

        var svc = new ProductImageThumbnailService(
            Options.Create(new ProductMediaOptions { ThumbnailEdgePixels = 120, WebpEncodingQuality = 80 }),
            NullLogger<ProductImageThumbnailService>.Instance);

        var webp = await svc.CreateSquareThumbnailWebpAsync(pngBytes, CancellationToken.None);

        Assert.NotEmpty(webp);
        using var result = Image.Load(webp);
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
}
