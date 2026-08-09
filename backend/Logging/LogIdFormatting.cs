namespace KasseAPI_Final.Logging;

/// <summary>Short, unmasked id fragments for readable console logs (full ids remain in structured scopes when present).</summary>
public static class LogIdFormatting
{
    /// <summary>First 8 hex chars of a Guid (no dashes), e.g. <c>b0000001</c>.</summary>
    public static string ShortGuid(Guid id) => id.ToString("N")[..8];

    /// <summary>First 8 chars of a Guid string or opaque id; returns the value as-is when shorter.</summary>
    public static string ShortId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "-";

        var trimmed = id.Trim();
        if (Guid.TryParse(trimmed, out var guid))
            return ShortGuid(guid);

        return trimmed.Length <= 8 ? trimmed : trimmed[..8];
    }

    /// <summary>
    /// Standard user log label: <c>email-or-username (shortId)</c>.
    /// Empty user id → <c>system</c>; missing label → <c>unknown (shortId)</c>.
    /// </summary>
    public static string FormatUser(string? emailOrUsername, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "system";

        var label = string.IsNullOrWhiteSpace(emailOrUsername) ? "unknown" : emailOrUsername.Trim();
        return $"{label} ({ShortId(userId)})";
    }

    /// <summary>Standard tenant log label: <c>slug (shortId)</c>.</summary>
    public static string FormatTenant(string? slug, Guid? tenantId)
    {
        var label = string.IsNullOrWhiteSpace(slug) ? "-" : slug.Trim();
        if (!tenantId.HasValue)
            return label;

        return $"{label} ({ShortGuid(tenantId.Value)})";
    }

    /// <summary>Standard tenant log label from string id: <c>slug (shortId)</c>.</summary>
    public static string FormatTenant(string? slug, string? tenantId)
    {
        var label = string.IsNullOrWhiteSpace(slug) ? "-" : slug.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
            return label;

        return $"{label} ({ShortId(tenantId)})";
    }
}
