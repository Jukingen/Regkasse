namespace KasseAPI_Final.Configuration;

/// <summary>
/// Local static hosting for one-click generated tenant websites (ProductMedia-style public files).
/// There is no external CDN in the default deployment; <see cref="PublicBaseUrl"/> can point at a CDN origin later.
/// </summary>
public sealed class WebsiteGeneratorOptions
{
    public const string SectionName = "WebsiteGenerator";

    /// <summary>Directory under content root where generated site files are stored.</summary>
    public string RootRelativeDirectory { get; set; } = "App_Data/generated-websites";

    /// <summary>Optional public origin (e.g. https://cdn.example.com). Empty: relative public path only.</summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>Must match <c>StaticFileOptions.RequestPath</c> (leading slash, no trailing slash).</summary>
    public string PublicUrlPathPrefix { get; set; } = "/media/sites";

    /// <summary>When false, generate endpoints return 503 with a stable code.</summary>
    public bool Enabled { get; set; } = true;
}
