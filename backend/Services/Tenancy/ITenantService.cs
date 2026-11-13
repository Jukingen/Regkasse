using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.AdminTenants;

namespace KasseAPI_Final.Services.Tenancy;

/// <summary>Tenant lifecycle (soft delete, restore, permanent delete, status transitions) for SaaS mandants.</summary>
public interface ITenantService
{
    Task<(bool Success, string? Error)> SoftDeleteAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> RestoreAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    Task<TenantPermanentDeleteResult> HardDeleteAsync(
        Guid tenantId,
        HardDeleteAdminTenantRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>InOnboarding → Active when onboarding completes.</summary>
    Task<(bool Success, string? Error)> CompleteOnboardingAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Active → Suspended when license expires.</summary>
    Task<(bool Success, string? Error)> SuspendForExpiredLicenseAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Cancelled → Archived when retention (default 30 days) elapsed.</summary>
    Task<int> ArchiveExpiredCancellationsAsync(
        TimeSpan retention,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Super Admin: set any known lifecycle status.</summary>
    Task<(bool Success, string? Error)> SetStatusAsync(
        Guid tenantId,
        TenantStatus status,
        string? actorUserId,
        CancellationToken cancellationToken = default);
}
