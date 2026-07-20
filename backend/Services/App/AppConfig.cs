namespace KasseAPI_Final.Services.App;

/// <summary>Branding + catalog snapshot used to generate a tenant mobile app.</summary>
public sealed class AppConfig
{
    public Guid TenantId { get; init; }
    public required string AppName { get; init; }
    public required string Slug { get; init; }
    public required AppColorPalette Colors { get; init; }
    public string? Logo { get; init; }
    public string? Description { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public IReadOnlyList<AppMenuItem> Menu { get; init; } = [];
    public IReadOnlyList<AppCategoryItem> Categories { get; init; } = [];

    /// <summary>
    /// Live menu endpoint path (relative to API origin) shared with dynamic websites.
    /// PWA may refresh catalog without regenerating static files.
    /// </summary>
    public string LiveMenuPath { get; init; } = string.Empty;
}
