namespace KasseAPI_Final.Services.Website;

/// <summary>In-memory preview of generated site assets (no deploy / no ZIP).</summary>
public sealed class WebsitePreviewResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public string? Html { get; private init; }
    public string? Css { get; private init; }
    public string? Js { get; private init; }
    public string? TemplateId { get; private init; }
    public string? TemplateName { get; private init; }
    public string? LogoUrl { get; private init; }
    public int MenuItemCount { get; private init; }
    public int CategoryCount { get; private init; }

    public static WebsitePreviewResult Success(
        string html,
        string css,
        string js,
        string templateId,
        string templateName,
        string? logoUrl,
        int menuItemCount,
        int categoryCount) =>
        new()
        {
            Succeeded = true,
            Html = html,
            Css = css,
            Js = js,
            TemplateId = templateId,
            TemplateName = templateName,
            LogoUrl = logoUrl,
            MenuItemCount = menuItemCount,
            CategoryCount = categoryCount
        };

    public static WebsitePreviewResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };
}
