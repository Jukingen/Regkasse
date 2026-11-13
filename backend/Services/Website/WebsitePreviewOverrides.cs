namespace KasseAPI_Final.Services.Website;

/// <summary>Unsaved FA form values applied only for preview generation (not persisted).</summary>
public sealed class WebsitePreviewOverrides
{
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TextColor { get; init; }
    public string? FontFamily { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public IReadOnlyList<string>? Pages { get; init; }
    public IReadOnlyList<string>? Features { get; init; }
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
}
