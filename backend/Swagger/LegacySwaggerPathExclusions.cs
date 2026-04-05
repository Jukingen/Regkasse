namespace KasseAPI_Final.Swagger;

/// <summary>
/// Central list of routes excluded from Swagger/OpenAPI output. Runtime routes may remain for backward compatibility
/// (see <see cref="KasseAPI_Final.Services.LegacyRouteDeprecationFilter"/>); they are hidden from the published contract.
/// </summary>
public static class LegacySwaggerPathExclusions
{
    /// <summary>
    /// Returns true if the API Explorer relative path (no leading slash, e.g. <c>api/pos/cart/current</c>)
    /// must not appear in generated OpenAPI documents.
    /// </summary>
    public static bool ShouldExclude(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return false;

        if (MatchesPrefix(relativePath, "api/Cart")
            || MatchesPrefix(relativePath, "api/Payment")
            || MatchesPrefix(relativePath, "api/Product"))
            return true;

        // Legacy simulated submit; operational flow is outbox + normal fiscal submit.
        if (relativePath.Equals("api/FinanzOnline/submit-invoice", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool MatchesPrefix(string path, string prefix) =>
        path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
        || path.Equals(prefix, StringComparison.OrdinalIgnoreCase);
}
