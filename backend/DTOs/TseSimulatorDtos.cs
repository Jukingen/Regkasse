namespace KasseAPI_Final.DTOs;

public sealed class TseSimulationResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Guid DeviceId { get; set; }
    public string? ScenarioId { get; set; }
    public string Message { get; set; } = string.Empty;
    public TseSimulationDeviceSnapshotDto? Device { get; set; }
}

public sealed class TseSimulationDeviceSnapshotDto
{
    public Guid Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool CanCreateInvoices { get; set; }
    public string CertificateStatus { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int HealthScore { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public int SimulatedLatencyMs { get; set; }
    public string? ActiveScenarioId { get; set; }
}

public sealed class TseSimulationScenarioDto
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = "Failure";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? FailureType { get; set; }
}

public sealed class TseSimulateFailureRequestDto
{
    public string FailureType { get; set; } = string.Empty;
}

public sealed class TseSimulateLatencyRequestDto
{
    public int LatencyMs { get; set; }
}

public sealed class TseSimulateCertificateExpiryRequestDto
{
    public DateTime ExpiryDateUtc { get; set; }
}
