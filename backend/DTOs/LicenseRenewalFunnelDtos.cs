namespace KasseAPI_Final.DTOs;

/// <summary>
/// Super Admin renewal conversion funnel (distinct tenants per step in a UTC window).
/// Cohort <see cref="Total"/> = tenants that received ≥1 license reminder in the period.
/// </summary>
public sealed record LicenseRenewalFunnelDto(
    int Total,
    int ReminderSent,
    int PageViewed,
    int Renewed,
    int Activated,
    /// <summary>Activated / Total * 100 (0 when Total is 0).</summary>
    double ConversionRate,
    DateTime FromUtc,
    DateTime ToUtc);

public sealed record LicenseRenewalFunnelQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);
