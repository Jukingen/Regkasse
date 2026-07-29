using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class DepExportRequirementResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RequirementType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public int Priority { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}

public sealed class DepExportCompliancePeriodResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ExportedAt { get; set; }
    public string? ExportedBy { get; set; }
    public string? FileName { get; set; }
    public string? FileHash { get; set; }
    public Guid? HistoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class DepExportComplianceStatusResponse
{
    public Guid TenantId { get; set; }
    public bool IsCompliant { get; set; }
    public int TotalRequirements { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public int OverdueCount { get; set; }
    public int LegalIncompleteCount { get; set; }
    public DepExportRequirementResponse? NextRequirement { get; set; }
    public DepExportCompliancePeriodResponse? CurrentPeriod { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
}

public static class DepExportComplianceDtoMapper
{
    public static DepExportRequirementResponse ToDto(DepExportRequirement requirement) =>
        new()
        {
            Id = requirement.Id,
            TenantId = requirement.TenantId,
            RequirementType = requirement.RequirementType,
            Title = requirement.Title,
            Description = requirement.Description,
            DueDate = requirement.DueDate,
            IsCompleted = requirement.IsCompleted,
            Priority = requirement.Priority,
            Category = requirement.Category,
            PeriodStart = requirement.PeriodStart,
            PeriodEnd = requirement.PeriodEnd,
        };

    public static DepExportCompliancePeriodResponse ToDto(DepExportCompliancePeriod period) =>
        new()
        {
            Id = period.Id,
            TenantId = period.TenantId,
            PeriodType = period.PeriodType,
            PeriodStart = period.PeriodStart,
            PeriodEnd = period.PeriodEnd,
            Status = period.Status,
            ExportedAt = period.ExportedAt,
            ExportedBy = period.ExportedBy,
            FileName = period.FileName,
            FileHash = period.FileHash,
            HistoryId = period.HistoryId,
            CreatedAt = period.CreatedAt,
            UpdatedAt = period.UpdatedAt,
        };

    public static DepExportComplianceStatusResponse ToDto(DepExportComplianceStatus status) =>
        new()
        {
            TenantId = status.TenantId,
            IsCompliant = status.IsCompliant,
            TotalRequirements = status.TotalRequirements,
            CompletedCount = status.CompletedCount,
            PendingCount = status.PendingCount,
            OverdueCount = status.OverdueCount,
            LegalIncompleteCount = status.LegalIncompleteCount,
            NextRequirement = status.NextRequirement is null ? null : ToDto(status.NextRequirement),
            CurrentPeriod = status.CurrentPeriod is null ? null : ToDto(status.CurrentPeriod),
            CheckedAtUtc = status.CheckedAtUtc,
            Disclaimer = status.Disclaimer,
        };
}
