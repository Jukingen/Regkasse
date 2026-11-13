namespace KasseAPI_Final.DTOs;

/// <summary>Single check / step inside a TSE developer-tools operation.</summary>
public sealed class TseDevToolCheckDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string Details { get; set; } = string.Empty;
    /// <summary>Info | Warning | Error</summary>
    public string Severity { get; set; } = "Info";
}

/// <summary>Result envelope for diagnostics / traffic / config / seed operations.</summary>
public sealed class TseDevToolResultDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    /// <summary>Diagnostics | SimulateTraffic | ValidateConfig | GenerateTestData</summary>
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public bool DevelopmentOnly { get; set; } = true;
    public IReadOnlyList<TseDevToolCheckDto> Results { get; set; } = Array.Empty<TseDevToolCheckDto>();
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}

public sealed class TseSimulateTrafficRequestDto
{
    /// <summary>Synthetic probe/sample count (1–1000). Not fiscal transactions.</summary>
    public int TransactionCount { get; set; } = 10;
}

public sealed class TseDeveloperToolsAvailabilityDto
{
    public bool Enabled { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
