namespace KasseAPI_Final.DTOs;

public sealed class CreateTseResourcePoolRequestDto
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Shared | Dedicated | Hybrid</summary>
    public string Type { get; set; } = "Shared";
    public int TotalCapacity { get; set; } = 10;
    public string? Description { get; set; }
    public IReadOnlyList<CreateTsePoolRuleRequestDto>? Rules { get; set; }
}

public sealed class CreateTsePoolRuleRequestDto
{
    public string RuleType { get; set; } = string.Empty;
    public string? RuleValue { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class AssignTenantToTsePoolRequestDto
{
    public Guid TenantId { get; set; }
    public Guid PoolId { get; set; }
    public int ReservedCapacity { get; set; } = 1;
}

public sealed class TseResourcePoolDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Shared";
    public int TotalCapacity { get; set; }
    public int UsedCapacity { get; set; }
    public int AvailableCapacity { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<Guid> AssignedTenants { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<TsePoolTenantSummaryDto> TenantSummaries { get; set; } =
        Array.Empty<TsePoolTenantSummaryDto>();
    public IReadOnlyList<TsePoolRuleDto> Rules { get; set; } = Array.Empty<TsePoolRuleDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class TsePoolTenantSummaryDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public int ReservedCapacity { get; set; }
    public DateTime AssignedAt { get; set; }
}

public sealed class TsePoolRuleDto
{
    public Guid Id { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string? RuleValue { get; set; }
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class TsePoolAssignmentResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PoolId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? PreviousPoolId { get; set; }
    public TseResourcePoolDto? Pool { get; set; }
}

public sealed class TsePoolStatusDto
{
    public Guid PoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Shared";
    public bool IsActive { get; set; }
    public int TotalCapacity { get; set; }
    public int UsedCapacity { get; set; }
    public int AvailableCapacity { get; set; }
    public double UtilizationPercent { get; set; }
    public int AssignedTenantCount { get; set; }
    public string HealthLabel { get; set; } = "Healthy";
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class TsePoolMetricsDto
{
    public Guid PoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int TotalCapacity { get; set; }
    public int UsedCapacity { get; set; }
    public int AvailableCapacity { get; set; }
    public double UtilizationPercent { get; set; }
    public int AssignedTenantCount { get; set; }
    public int ActiveDeviceCount { get; set; }
    public int HealthyDeviceCount { get; set; }
    public double AverageHealthScore { get; set; }
    public int SignedTransactionsLast30Days { get; set; }
    public IReadOnlyDictionary<string, int> DevicesByProvider { get; set; } =
        new Dictionary<string, int>();
}
