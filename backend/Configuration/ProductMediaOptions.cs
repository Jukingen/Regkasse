namespace KasseAPI_Final.Configuration;

/// <summary>
/// Local disk storage for admin-uploaded product images; public URL is written to <see cref="Models.Product.ImageUrl"/>.
/// </summary>
public sealed class ProductMediaOptions
{
    public const string SectionName = "ProductMedia";

    /// <summary>Directory under content root where images are stored.</summary>
    public string RootRelativeDirectory { get; set; } = "App_Data/product-images";

    /// <summary>Optional public origin (e.g. https://api.example.com). Empty: infer from current request (Scheme + Host).</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Max decoded image bytes (default 2 MiB).</summary>
    public long MaxBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>Square thumbnail edge length (pixels); center-crop, no stretch.</summary>
    public int ThumbnailEdgePixels { get; set; } = 120;

    /// <summary>WebP output quality (1–100).</summary>
    public int WebpEncodingQuality { get; set; } = 82;

    /// <summary>Must match <c>StaticFileOptions.RequestPath</c> (leading slash, no trailing slash).</summary>
    public string PublicUrlPathPrefix { get; set; } = "/media/product-images";
}
