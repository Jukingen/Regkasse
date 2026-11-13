namespace KasseAPI_Final.Data;

/// <summary>
/// Cash-register display numbers. Unique per tenant in the database, and unique across tenants
/// for seeded registers so demo mandants do not all share <c>KASSE-001</c>.
/// </summary>
public static class CashRegisterNumbers
{
    public const string Default = "KASSE-001";
    public const int MaxLength = 20;

    /// <summary>
    /// Slug <c>dev</c> keeps <see cref="Default"/>. Every other tenant gets <c>K-{slug}</c> when that
    /// fits in <see cref="MaxLength"/>, otherwise a tenant-id fragment.
    /// </summary>
    public static string ForTenant(Guid tenantId, string? slug)
    {
        if (string.Equals(slug?.Trim(), "dev", StringComparison.OrdinalIgnoreCase))
            return Default;

        var compact = CompactSlug(slug);
        if (compact.Length is > 0 and <= 18)
            return $"K-{compact}";

        return "K-" + tenantId.ToString("N")[..18].ToUpperInvariant();
    }

    private static string CompactSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return string.Empty;

        var chars = slug.Trim().ToUpperInvariant()
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == '-')
            .ToArray();
        return new string(chars);
    }
}
