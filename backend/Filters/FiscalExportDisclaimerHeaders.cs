namespace KasseAPI_Final.Filters;

public static class FiscalExportDisclaimerHeaders
{
    public const string AcknowledgedHeaderName = "X-Disclaimer-Acknowledged";

    public static bool IsAcknowledged(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(AcknowledgedHeaderName, out var values))
            return false;

        var raw = values.FirstOrDefault();
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
    }
}
