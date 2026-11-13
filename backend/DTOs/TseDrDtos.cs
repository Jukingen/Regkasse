namespace KasseAPI_Final.DTOs;

public sealed class GenerateTseDrRunbookRequestDto
{
    /// <summary>TSEFailure | NetworkIsolation | DataCorruption</summary>
    public string Scenario { get; set; } = "TSEFailure";
}

public sealed class TseDrStepDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsAutomated { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
}

public sealed class TseDrRunbookDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scenario { get; set; } = "TSEFailure";
    public string Status { get; set; } = "Ready";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public int EstimatedRtoMinutes { get; set; }
    public int ActualRtoMinutes { get; set; }
    public bool IsDrill { get; set; }
    public string? Summary { get; set; }
    public IReadOnlyList<TseDrStepDto> Steps { get; set; } = Array.Empty<TseDrStepDto>();
}

public sealed class TseDrExecutionResultDto
{
    public Guid RunbookId { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "Completed";
    public bool Success { get; set; }
    public int ActualRtoMinutes { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public int SkippedManualSteps { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool SimulationOnly { get; set; } = true;
    public TseDrRunbookDto Runbook { get; set; } = new();
}

public sealed class TseDrStatusDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool IsReady { get; set; }
    public DateTime? LastDrillAt { get; set; }
    public int RtoTargetMinutes { get; set; }
    public int RtoActualMinutes { get; set; }
    public int PrimaryDeviceCount { get; set; }
    public int HealthyBackupCount { get; set; }
    public string ReadinessMessage { get; set; } = string.Empty;
    public Guid? LatestRunbookId { get; set; }
    public TseDrRunbookDto? LatestRunbook { get; set; }
}

public sealed class TseDrReportDto
{
    public Guid TenantId { get; set; }
    public Guid RunbookId { get; set; }
    public string Scenario { get; set; } = "TSEFailure";
    public bool Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int ActualRtoMinutes { get; set; }
    public int RtoTargetMinutes { get; set; }
    public bool MetRtoTarget { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> Findings { get; set; } = Array.Empty<string>();
    public TseDrExecutionResultDto Execution { get; set; } = new();
    public TseDrStatusDto StatusAfter { get; set; } = new();
}
