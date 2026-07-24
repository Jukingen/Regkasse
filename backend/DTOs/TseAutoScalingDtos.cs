namespace KasseAPI_Final.DTOs;

public sealed class TseScalingPolicyDto
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
    public int MinDevices { get; set; } = 1;
    public int MaxDevices { get; set; } = 5;
    public int TargetTransactionsPerDevice { get; set; } = 5000;
    public double ScaleUpThreshold { get; set; } = 80;
    public double ScaleDownThreshold { get; set; } = 30;
    public int CooldownMinutes { get; set; } = 30;
    /// <summary>Soft backup stub provision/deactivation. Default false = recommend-only.</summary>
    public bool AutoProvision { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ConfigureTseScalingPolicyRequestDto
{
    public bool Enabled { get; set; }
    public int MinDevices { get; set; } = 1;
    public int MaxDevices { get; set; } = 5;
    public int TargetTransactionsPerDevice { get; set; } = 5000;
    public double ScaleUpThreshold { get; set; } = 80;
    public double ScaleDownThreshold { get; set; } = 30;
    public int CooldownMinutes { get; set; } = 30;
    public bool AutoProvision { get; set; }
}

public sealed class TseScalingResultDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string Action { get; set; } = "NoOp";
    public int CurrentDevices { get; set; }
    public int RecommendedDevices { get; set; }
    public double CurrentLoadPercent { get; set; }
    public bool Applied { get; set; }
    public bool SimulationOnly { get; set; } = true;
    public string Reason { get; set; } = string.Empty;
    public TseScalingPolicyDto Policy { get; set; } = new();
}

public sealed class TseScalingHistoryItemDto
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public int From { get; set; }
    public int To { get; set; }
    public double LoadPercent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool Applied { get; set; }
    public bool SimulationOnly { get; set; }
    public string? ActorUserId { get; set; }
}

public sealed class TseScalingHistoryDto
{
    public Guid TenantId { get; set; }
    public IReadOnlyList<TseScalingHistoryItemDto> Items { get; set; } =
        Array.Empty<TseScalingHistoryItemDto>();
}

public sealed class TseScalingStatusDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public bool ScalingEnabled { get; set; }
    public int CurrentDevices { get; set; }
    public int RecommendedDevices { get; set; }
    public double CurrentLoadPercent { get; set; }
    public TseScalingPolicyDto Policy { get; set; } = new();
    public TseScalingHistoryItemDto? LastEvaluation { get; set; }
}
