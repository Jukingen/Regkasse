namespace KasseAPI_Final.DTOs;

public sealed class TseGatewayEndpointDto
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>healthy | unhealthy | unknown</summary>
    public string Status { get; set; } = "unknown";
    /// <summary>0–100 relative load from recent request share.</summary>
    public int Load { get; set; }
    public long Requests { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public double AvgResponseTimeMs { get; set; }
    public DateTime? LastCheckedAt { get; set; }
}

public sealed class TseGatewayConfigDto
{
    public string Strategy { get; set; } = "RoundRobin";
    public IReadOnlyList<TseGatewayEndpointDto> Endpoints { get; set; } =
        Array.Empty<TseGatewayEndpointDto>();
    public int HealthCheckInterval { get; set; } = 30;
    public int Timeout { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public bool Enabled { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ConfigureTseGatewayEndpointRequestDto
{
    public Guid? Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class ConfigureTseGatewayRequestDto
{
    public string Strategy { get; set; } = "RoundRobin";
    public List<ConfigureTseGatewayEndpointRequestDto> Endpoints { get; set; } = new();
    public int HealthCheckInterval { get; set; } = 30;
    public int Timeout { get; set; } = 5000;
    public int RetryCount { get; set; } = 3;
    public bool Enabled { get; set; } = true;
}

public sealed class TseGatewayRequestDto
{
    /// <summary>Only <c>HealthProbe</c> is supported (no fiscal Sign).</summary>
    public string Operation { get; set; } = "HealthProbe";
    public string? PreferredProvider { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class TseGatewayResponseDto
{
    public bool Success { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? SelectedProvider { get; set; }
    public string? SelectedEndpoint { get; set; }
    public Guid? SelectedEndpointId { get; set; }
    public int Attempts { get; set; }
    public long ElapsedMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public bool SimulationOnly { get; set; } = true;
}

public sealed class TseGatewayStatsDto
{
    public long TotalRequests { get; set; }
    public double SuccessRate { get; set; }
    public double AvgResponseTime { get; set; }
}

public sealed class TseGatewayStatusDto
{
    public bool Enabled { get; set; }
    public string Strategy { get; set; } = "RoundRobin";
    public TseGatewayStatsDto Stats { get; set; } = new();
    public IReadOnlyList<TseGatewayEndpointDto> Endpoints { get; set; } =
        Array.Empty<TseGatewayEndpointDto>();
    public DateTime GeneratedAt { get; set; }
}
