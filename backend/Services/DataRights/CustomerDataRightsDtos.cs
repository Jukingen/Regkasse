using KasseAPI_Final.Services.DataExport;

namespace KasseAPI_Final.Services.DataRights;

public sealed class DataRightsRequestTypeCatalogItemDto
{
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Approval { get; init; } = string.Empty;
    public string ProcessingTime { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public int? MaxProcessingHours { get; init; }
    public int? ConfirmationWaitDays { get; init; }
}

public sealed class CreateDataRightsRequestDto
{
    /// <summary><c>view</c> | <c>export</c> | <c>delete</c></summary>
    public string Type { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class TenantDataRightsRequestDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public string? RequestedByUserId { get; init; }
    public DateTime RequestedAtUtc { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public DateTime? ProcessingDeadlineUtc { get; init; }
    public DateTime? ReadyAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? ArtifactFileName { get; init; }
    public long? ArtifactByteSize { get; init; }
    public string? DownloadLink { get; init; }
    public DateTime? DownloadExpiresAtUtc { get; init; }
    public bool CanDownload { get; init; }
    public bool CanConfirm { get; init; }
    public bool CanExecute { get; init; }
    public Guid? LinkedDeletionRequestId { get; init; }
    public TenantDataDeletionRequestDto? LinkedDeletionRequest { get; init; }
    public TenantDataManagementSummaryDto? ViewSummary { get; set; }
    public string? ErrorMessage { get; init; }
    public int? ConfirmationWaitDays { get; init; }
}

public sealed class DataRightsExportDownload
{
    public required string FileName { get; init; }
    public required byte[] Data { get; init; }
}
