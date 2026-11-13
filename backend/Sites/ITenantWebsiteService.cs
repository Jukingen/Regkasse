namespace KasseAPI_Final.Sites;

public interface ITenantWebsiteService
{
    /// <summary>
    /// Live HTML for a tenant slug (menu + profile). Null → not found.
    /// Optional <paramref name="templateId"/>: modern | classic | minimal.
    /// </summary>
    Task<string?> GetWebsiteHtmlAsync(
        string slug,
        string? templateId = null,
        CancellationToken ct = default);
}
