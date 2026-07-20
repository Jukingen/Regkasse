using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Services.App;

namespace KasseAPI_Final.DTOs;

public sealed class WebsiteTemplateDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PreviewImage { get; init; }
}

public sealed class GenerateWebsiteRequestDto
{
    /// <summary>Required for Super Admin without ambient tenant; ignored for Mandanten-Admin (JWT tenant used).</summary>
    public Guid? TenantId { get; init; }

    [Required]
    [MaxLength(32)]
    public string TemplateId { get; init; } = string.Empty;
}

public sealed class GenerateWebsiteResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? Url { get; init; }
    public string? TemplateId { get; init; }
    public string? TemplateName { get; init; }
    public int MenuItemCount { get; init; }
    public int CategoryCount { get; init; }
    public int? ProgressPercent { get; init; }
    public string? ProgressStage { get; init; }
}

public sealed class PreviewWebsiteRequestDto
{
    public Guid? TenantId { get; init; }

    [Required]
    [MaxLength(32)]
    public string TemplateId { get; init; } = "modern";

    /// <summary>Optional unsaved FA form overrides applied only for this preview (not persisted).</summary>
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? BackgroundColor { get; init; }
    public string? TextColor { get; init; }
    public string? FontFamily { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public List<string>? Pages { get; init; }
    public List<string>? Features { get; init; }
    public string? CustomCss { get; init; }
    public string? CustomJs { get; init; }
}

public sealed class PreviewWebsiteResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? Html { get; init; }
    public string? Css { get; init; }
    public string? Js { get; init; }
    public string? TemplateId { get; init; }
    public string? TemplateName { get; init; }
    public string? LogoUrl { get; init; }
    public int MenuItemCount { get; init; }
    public int CategoryCount { get; init; }
}

public sealed class GenerateMobileAppRequestDto
{
    public Guid? TenantId { get; init; }

    /// <summary><see cref="AppType"/>: <c>Pwa</c> or <c>Native</c>. Defaults to PWA.</summary>
    public AppType AppType { get; init; } = AppType.Pwa;
}

public sealed class GenerateMobileAppResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public string? Url { get; init; }
    public AppType? AppType { get; init; }
}
