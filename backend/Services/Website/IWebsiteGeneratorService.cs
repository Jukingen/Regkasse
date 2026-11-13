namespace KasseAPI_Final.Services.Website;

public interface IWebsiteGeneratorService
{
    IReadOnlyList<WebsiteTemplate> GetTemplates();

    Task<WebsiteResult> GenerateWebsiteAsync(
        Guid tenantId,
        string templateId,
        CancellationToken ct = default);

    /// <summary>Build HTML/CSS/JS for FA preview without writing files to disk.</summary>
    Task<WebsitePreviewResult> PreviewWebsiteAsync(
        Guid tenantId,
        string templateId,
        WebsitePreviewOverrides? overrides = null,
        CancellationToken ct = default);
}
