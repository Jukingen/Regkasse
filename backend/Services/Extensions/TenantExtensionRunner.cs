namespace KasseAPI_Final.Services.Extensions;

/// Runs enabled extensions for one hook. A null registry is a no-op so existing
/// flows stay unchanged when no extensions are registered.
/// Returns the last <see cref="ExtensionResult.ModifiedPayload"/> when an extension adjusted the cart.
public static class TenantExtensionRunner
{
    public static async Task<IReadOnlyDictionary<string, object?>?> RunAsync(
        ITenantExtensionRegistry? registry,
        Guid tenantId,
        string hookName,
        IReadOnlyDictionary<string, object?>? payload,
        CancellationToken cancellationToken)
    {
        if (registry is null || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(hookName))
            return null;

        var extensions = registry.GetForHook(tenantId, hookName);
        if (extensions.Count == 0)
            return null;

        var current = payload ?? new Dictionary<string, object?>();
        IReadOnlyDictionary<string, object?>? modified = null;
        foreach (var ext in extensions)
        {
            var context = new ExtensionContext(tenantId, hookName, current);
            var result = await ext.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            if (!result.Success)
                throw new ExtensionFailedException(ext.FeatureName, result.Message);
            if (result.ModifiedPayload is not null)
            {
                modified = result.ModifiedPayload;
                current = result.ModifiedPayload;
            }
        }

        return modified;
    }
}
