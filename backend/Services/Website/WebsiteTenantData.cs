namespace KasseAPI_Final.Services.Website;

/// <summary>Tenant + company identity snapshot used for HTML/CSS/JS generation (no secrets).</summary>
public sealed class WebsiteTenantData
{
    /// <summary>Relative path written next to generated site files when no tenant logo exists.</summary>
    public const string DefaultLogoRelativePath = "assets/default-logo.svg";

    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? CompanyName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? Description { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TextColor { get; init; }
    public string? FontFamily { get; init; }
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
    public IReadOnlyList<string> Pages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WebsiteMenuCategory> Categories { get; init; } = Array.Empty<WebsiteMenuCategory>();
    public IReadOnlyList<WebsiteMenuItem> MenuItems { get; init; } = Array.Empty<WebsiteMenuItem>();
    public IReadOnlyDictionary<string, string> BusinessHours { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool HasLiveMenu =>
        Features.Any(f => string.Equals(f, "live-menu", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Category snapshot embedded in generated static sites.</summary>
public sealed class WebsiteMenuCategory
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Product/menu row snapshot embedded in generated static sites.</summary>
public sealed class WebsiteMenuItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public string? Description { get; init; }
}
