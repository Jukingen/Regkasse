namespace KasseAPI_Final.Services.AdminTenants;

/// <summary>
/// UI-oriented mandant license status for Super Admin overview (matches FA <c>resolveMandantLicenseOverviewStatus</c>).
/// </summary>
public static class TenantLicenseOverviewStatusMapper
{
    public const int ExpiringSoonDays = 7;

    public static string ResolveStatus(
        DateTime? validUntilUtc,
        string? licenseKey,
        DateTime? nowUtc = null)
    {
        var hasKey = !string.IsNullOrWhiteSpace(licenseKey);
        var hasUntil = validUntilUtc.HasValue;

        if (!hasUntil && !hasKey)
            return "no_license";

        if (!hasUntil)
            return "no_license";

        var now = nowUtc ?? DateTime.UtcNow;
        var until = DateTime.SpecifyKind(validUntilUtc!.Value, DateTimeKind.Utc);
        var days = (int)Math.Ceiling((until - now).TotalDays);

        if (days < 0)
            return "expired";

        if (!hasKey)
            return "trial";

        if (days <= ExpiringSoonDays)
            return "expiring_soon";

        return "active";
    }
}
