namespace KasseAPI_Final.Models.DTOs;

public sealed class DevTenantCatalogCleanupRequest
{
    public Guid? TenantId { get; init; }
    public string? TenantSlug { get; init; }
    public bool IncludeCategories { get; init; } = true;
    public string ConfirmPhrase { get; init; } = string.Empty;
}
