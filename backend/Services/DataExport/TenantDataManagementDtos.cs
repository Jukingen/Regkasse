using KasseAPI_Final.Services.DataRetention;
using KasseAPI_Final.Services.License;

namespace KasseAPI_Final.Services.DataExport;

public sealed class TenantDataManagementSummaryDto
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
    public int RksvRetentionYears { get; init; } = 7;
    public string RksvRetentionNote { get; init; } = string.Empty;
    public bool CanExport { get; init; }
    public bool CanRequestDeletion { get; init; }
    public bool CanConfirmDeletion { get; init; }
    public bool CanExecutePurge { get; init; }
    public IReadOnlyList<TenantDataTypeSummaryDto> DataTypes { get; init; } = Array.Empty<TenantDataTypeSummaryDto>();
    public TenantDataDeletionRequestDto? LatestDeletionRequest { get; init; }
    public RetentionReport? Retention { get; init; }
}

public sealed class TenantDataTypeSummaryDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public bool IsRksvRetained { get; init; }
    public bool DeletedOnPurge { get; init; }
}

public sealed class TenantDataDeletionRequestDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? ExportCompletedAtUtc { get; init; }
    public DateTime? ConfirmedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime? PurgeEligibleAtUtc { get; init; }
    public string? ExecutedVia { get; init; }
    public int ConfirmationWaitDays { get; init; } = 7;
}

public sealed class RequestTenantDataDeletionDto
{
    public string? Reason { get; set; }
}
