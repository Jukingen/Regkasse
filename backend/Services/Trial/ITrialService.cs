using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Trial;

public sealed record TrialTenantSummaryDto(
    Guid TenantId,
    string Name,
    string Slug,
    string? Email,
    string? TrialStatus,
    DateTime? TrialStartedAtUtc,
    DateTime? TrialEndsAtUtc,
    DateTime? TrialGracePeriodEndsAtUtc,
    DateTime? TrialConvertedAtUtc,
    DateTime? TrialDeletedAtUtc,
    int? DaysRemaining,
    bool Reminder7dSent,
    bool Reminder3dSent,
    bool Reminder1dSent);

public sealed record TrialDashboardDto(
    int ActiveCount,
    int ExpiringSoonCount,
    int ExpiredCount,
    int ConvertedCount,
    int DeletedCount,
    double ConversionRatePercent,
    IReadOnlyList<TrialTenantSummaryDto> ActiveTrials,
    IReadOnlyList<TrialTenantSummaryDto> ExpiringSoon,
    IReadOnlyList<TrialTenantSummaryDto> ExpiredTrials);

public sealed class ExtendTrialRequest
{
    /// <summary>Days to add from now or from current trial end (whichever is later).</summary>
    public int AdditionalDays { get; set; } = 14;
}

public sealed class GrantTrialRequest
{
    public int? DurationDays { get; set; }
}

public interface ITrialService
{
    /// <summary>
    /// Applies managed trial columns + syncs <see cref="Tenant.LicenseValidUntilUtc"/>.
    /// Caller persists the tenant (or this method saves when <paramref name="saveChanges"/> is true).
    /// </summary>
    void ApplyTrialGrant(Tenant tenant, int durationDays, DateTime nowUtc);

    int ResolveDurationDays(int? requestedDays);

    Task<TrialDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<TrialAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);

    Task<TrialTenantSummaryDto?> GetTenantTrialAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<(TrialTenantSummaryDto? Result, string? Error)> GrantOrRestartTrialAsync(
        Guid tenantId,
        int? durationDays,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(TrialTenantSummaryDto? Result, string? Error)> ExtendTrialAsync(
        Guid tenantId,
        int additionalDays,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Delegates to <see cref="ITrialConversionService"/> (keeps remaining trial days by default).</summary>
    Task<(TrialTenantSummaryDto? Result, string? Error)> ConvertToPaidAsync(
        Guid tenantId,
        Guid licenseSaleId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> SoftDeleteTrialAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Mark active trials past end as expired + start grace; send expired email once.</summary>
    Task<int> ProcessExpiryAndGraceAsync(CancellationToken cancellationToken = default);

    /// <summary>Send 7/3/1 day reminder emails for active trials.</summary>
    Task<int> ProcessRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-archive expired trials whose grace ended more than <c>AutoDeleteAfterGraceDays</c> ago.
    /// Does not hard-delete fiscal/RKSV rows.
    /// </summary>
    Task<int> ProcessCleanupAsync(CancellationToken cancellationToken = default);
}
