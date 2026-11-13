namespace KasseAPI_Final.DTOs;

public sealed class TseTrainingModuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public string Category { get; set; } = "Basics";
    public bool IsStarted { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class TseTrainingEnvironmentDto
{
    public IReadOnlyList<TseTrainingModuleDto> Modules { get; set; } = Array.Empty<TseTrainingModuleDto>();
    public int CompletedCount { get; set; }
    public int TotalCount { get; set; }
    public bool SimulationEnabled { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseTrainingConsoleEntryDto
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Level { get; set; } = "info";
    public string Scenario { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public bool Success { get; set; }
}

public sealed class TseTrainingSimulateRequestDto
{
    public Guid DeviceId { get; set; }
    /// <summary>NetworkTimeout | CertificateExpiry | SignatureError (and other simulator failure names).</summary>
    public string FailureType { get; set; } = string.Empty;
}

public sealed class TseTrainingSimulateResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public TseSimulationDeviceSnapshotDto? Device { get; set; }
    public TseTrainingConsoleEntryDto? ConsoleEntry { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}
