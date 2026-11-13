namespace KasseAPI_Final.DTOs;

public sealed class CreateTseIncidentRequestDto
{
    public Guid TenantId { get; set; }
    public Guid? DeviceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Critical | High | Medium | Low</summary>
    public string Severity { get; set; } = "Medium";
    public DateTime? DetectedAt { get; set; }
}

public sealed class UpdateTseIncidentStatusRequestDto
{
    /// <summary>Open | Investigating | Resolved | Closed</summary>
    public string Status { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string? Note { get; set; }
}

public sealed class AddTseIncidentActionRequestDto
{
    public string ActionType { get; set; } = "Other";
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public sealed class TseIncidentDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<TseIncidentLogDto> Logs { get; set; } = Array.Empty<TseIncidentLogDto>();
    public IReadOnlyList<TseIncidentActionDto> Actions { get; set; } = Array.Empty<TseIncidentActionDto>();
}

public sealed class TseIncidentLogDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class TseIncidentActionDto
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; }
    public bool IsCompleted { get; set; }
}

public sealed class TseIncidentReportDto
{
    public Guid IncidentId { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public TimeSpan? TimeToResolve { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public int LogCount { get; set; }
    public int ActionCount { get; set; }
    public int CompletedActionCount { get; set; }
    public DateTime GeneratedAt { get; set; }
    public IReadOnlyList<TseIncidentLogDto> Timeline { get; set; } = Array.Empty<TseIncidentLogDto>();
    public IReadOnlyList<TseIncidentActionDto> Actions { get; set; } = Array.Empty<TseIncidentActionDto>();
}

public sealed class TseIncidentDashboardDto
{
    public int OpenCount { get; set; }
    public int InvestigatingCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }
    public int CriticalOpenCount { get; set; }
    public IReadOnlyList<TseIncidentDto> Incidents { get; set; } = Array.Empty<TseIncidentDto>();
}
