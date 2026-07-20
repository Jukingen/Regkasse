namespace KasseAPI_Final.DTOs;

public sealed class TenantCustomizationDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    /// <summary><c>website</c> or <c>app</c> (sketch field name: Type).</summary>
    public string Type { get; init; } = "website";
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TextColor { get; init; }
    public string? FontFamily { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public IReadOnlyList<string> Pages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class UpsertTenantCustomizationRequestDto
{
    public Guid? TenantId { get; set; }

    /// <summary><c>website</c> or <c>app</c>.</summary>
    public string Type { get; set; } = "website";

    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    public string? FontFamily { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public List<string>? Pages { get; set; }
    public List<string>? Features { get; set; }
    public string? CustomCss { get; set; }
    public string? CustomJs { get; set; }
}
