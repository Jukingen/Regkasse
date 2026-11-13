namespace KasseAPI_Final.DTOs;

/// <summary>One mandant currently inside the license grace window (Super Admin overview).</summary>
public sealed record GracePeriodTenantRowDto(
    Guid Id,
    string Name,
    string Slug,
    DateTime ExpiredAtUtc,
    int DaysRemaining,
    DateTime LockdownAtUtc);

/// <summary>
/// Super Admin grace-period cohort: bucket counts + tenant rows ordered by urgency.
/// </summary>
public sealed record GracePeriodDashboardDto(
    int Total,
    int Critical,
    int Medium,
    int Good,
    IReadOnlyList<GracePeriodTenantRowDto> List);
