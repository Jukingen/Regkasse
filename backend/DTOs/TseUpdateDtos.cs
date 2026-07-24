namespace KasseAPI_Final.DTOs;

public sealed class TseUpdateStatusDto
{
    public Guid TenantId { get; set; }
    public bool HasUpdates { get; set; }
    public IReadOnlyList<TseAvailableUpdateDto> AvailableUpdates { get; set; } =
        Array.Empty<TseAvailableUpdateDto>();
    public DateTime LastChecked { get; set; }
    public string RiskLevel { get; set; } = "Low";
    public int ActiveDeviceCount { get; set; }
    public bool ZeroDowntimeCapable { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseAvailableUpdateDto
{
    public string UpdateType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string Risk { get; set; } = "Low";
    public bool RequiresHealthyBackup { get; set; }
    public bool ZeroDowntime { get; set; } = true;
}

public sealed class TseUpdateResultDto
{
    public Guid TenantId { get; set; }
    public string UpdateType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? FromVersion { get; set; }
    public string? ToVersion { get; set; }
    public bool ZeroDowntime { get; set; }
    public int DevicesTouched { get; set; }
    public Guid? HistoryId { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseUpdateHistoryDto
{
    public Guid TenantId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public IReadOnlyList<TseUpdateHistoryItemDto> Items { get; set; } =
        Array.Empty<TseUpdateHistoryItemDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseUpdateHistoryItemDto
{
    public Guid Id { get; set; }
    public string UpdateType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ZeroDowntime { get; set; }
    public int DevicesTouched { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Message { get; set; }
}

public sealed class TseApplyUpdateRequestDto
{
    public string UpdateType { get; set; } = string.Empty;
}
