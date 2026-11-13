namespace KasseAPI_Final.Swagger;

/// <summary>
/// Central list of routes excluded from Swagger/OpenAPI output.
/// <c>/api/Cart</c>, <c>/api/Payment</c>, and <c>/api/Product</c> aliases were hard-removed (2026-08-13);
/// exclusions remain as a safety net if a dual route is reintroduced.
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

        // Other retired aliases — canonical paths remain in OpenAPI.
        if (MatchesPrefix(relativePath, "api/CompanySettings")
            || MatchesPrefix(relativePath, "api/pos/company-profile")
            || MatchesPrefix(relativePath, "api/pos/payment/card"))
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
