using KasseAPI_Final.Services.License;

namespace KasseAPI_Final.Services.DataExport;

/// <summary>Super Admin cross-tenant data-management row (lifecycle + RKSV retention + deletion requests).</summary>
public sealed class TenantDataManagementOverviewItemDto
{
    public Guid TenantId { get; init; }
    public string TenantSlug { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string LifecycleState { get; init; } = LicenseLifecycleState.Active.ToString();
    public DateTime? LicenseValidUntilUtc { get; init; }
    public int DaysOverdue { get; init; }
    public bool IsInGracePeriod { get; init; }
    public int GracePeriodRemainingDays { get; init; }
    public bool IsLocked { get; init; }
    public bool IsArchived { get; init; }
    public DateTime? CustomerDataPurgedAtUtc { get; init; }
    public bool HasPendingDeletionRequest { get; init; }
    public string? DeletionRequestStatus { get; init; }
    public DateTime? DeletionRequestedAtUtc { get; init; }
    public DateTime? OldestRksvPaymentDate { get; init; }
    public DateTime? RksvRetentionUntil { get; init; }
    public int RksvPaymentCount { get; init; }
}

public sealed class TenantDataManagementOverviewDto
{
    public IReadOnlyList<TenantDataManagementOverviewItemDto> Items { get; init; } =
        Array.Empty<TenantDataManagementOverviewItemDto>();

    public int TotalTenants { get; init; }
    public int InGraceCount { get; init; }
    public int LockedCount { get; init; }
    public int PendingDeletionRequestCount { get; init; }
    public int PurgedCount { get; init; }
}
