namespace KasseAPI_Final.Services.Website;

/// <summary>Built-in one-click website template metadata (not persisted).</summary>
public sealed class WebsiteTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PreviewImage { get; init; }
}
