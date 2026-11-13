namespace KasseAPI_Final.Services.Website;

/// <summary>Outcome of <see cref="IWebsiteGeneratorService.GenerateWebsiteAsync"/>.</summary>
public sealed class WebsiteResult
{
    public bool Succeeded { get; private init; }
    public string? Code { get; private init; }
    public string? Error { get; private init; }
    public string? Url { get; private init; }
    public string? TemplateName { get; private init; }
    public string? TemplateId { get; private init; }
    public WebsiteGenerateProgress? Progress { get; private init; }
    public int MenuItemCount { get; private init; }
    public int CategoryCount { get; private init; }

    public static WebsiteResult Success(
        string url,
        string templateName,
        string templateId,
        WebsiteGenerateProgress? progress = null,
        int menuItemCount = 0,
        int categoryCount = 0) =>
        new()
        {
            Succeeded = true,
            Url = url,
            TemplateName = templateName,
            TemplateId = templateId,
            Progress = progress,
            MenuItemCount = menuItemCount,
            CategoryCount = categoryCount
        };

    public static WebsiteResult Fail(string code, string error) =>
        new()
        {
            Succeeded = false,
            Code = code,
            Error = error
        };

    /// <summary>Convenience overload matching the product sketch (<c>Fail("Template not found")</c>).</summary>
    public static WebsiteResult Fail(string error) => Fail("WEBSITE_GENERATE_FAILED", error);
}
