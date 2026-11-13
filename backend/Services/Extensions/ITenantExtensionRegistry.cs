namespace KasseAPI_Final.Services.Extensions;

public interface ITenantExtensionRegistry
{
    IReadOnlyList<ITenantFeatureExtension> GetForHook(Guid tenantId, string hookName);
}
