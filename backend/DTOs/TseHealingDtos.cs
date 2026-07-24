namespace KasseAPI_Final.DTOs;

public sealed class TseHealingConfigurationDto
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
    public int MaxAutoHealAttempts { get; set; } = 3;
    public int CooldownMinutes { get; set; } = 30;
    public bool NotifyOnHeal { get; set; } = true;
    public bool AllowAutoFailover { get; set; }
    public IReadOnlyList<TseHealingRuleDto> Rules { get; set; } = Array.Empty<TseHealingRuleDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseHealingRuleDto
{
    public Guid Id { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = "Enabled";
    public DateTime? LastTriggeredAt { get; set; }
}

public sealed class ConfigureTseHealingRequestDto
{
    public bool Enabled { get; set; }
    public int MaxAutoHealAttempts { get; set; } = 3;
    public int CooldownMinutes { get; set; } = 30;
    public bool NotifyOnHeal { get; set; } = true;
    public bool AllowAutoFailover { get; set; }
    public IReadOnlyList<ConfigureTseHealingRuleDto>? Rules { get; set; }
}

public sealed class ConfigureTseHealingRuleDto
{
    public Guid? Id { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public string Status { get; set; } = "Enabled";
}

public sealed class TseHealingResultDto
{
    public Guid DeviceId { get; set; }
    public Guid? TenantId { get; set; }
    public bool Success { get; set; }
    public bool Applied { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int HealthScoreBefore { get; set; }
    public int? HealthScoreAfter { get; set; }
    public string? MatchedCondition { get; set; }
    public string? ActionTaken { get; set; }
    public Guid? HistoryId { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseHealingReportDto
{
    public Guid TenantId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int TotalAttempts { get; set; }
    public int AppliedCount { get; set; }
    public int SucceededCount { get; set; }
    public IReadOnlyList<TseHealingHistoryItemDto> Items { get; set; } =
        Array.Empty<TseHealingHistoryItemDto>();
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseHealingHistoryItemDto
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public string? Condition { get; set; }
    public string? Action { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool Applied { get; set; }
    public int HealthScoreBefore { get; set; }
    public int? HealthScoreAfter { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
