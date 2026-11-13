namespace KasseAPI_Final.DTOs;

public sealed class TenantTseDeviceStatusDto
{
    public Guid DeviceId { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsBackup { get; set; }
    public string HealthStatus { get; set; } = "Unknown";
    public int HealthScore { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public DateTime? LastHealthCheck { get; set; }
}

public sealed class TenantTseStatusDto
{
    public Guid TenantId { get; set; }
    public IReadOnlyList<TenantTseDeviceStatusDto> Devices { get; set; } =
        Array.Empty<TenantTseDeviceStatusDto>();
    /// <summary>Healthy | Degraded | Unknown</summary>
    public string OverallHealth { get; set; } = "Unknown";
    public int OverallHealthScore { get; set; }
    public int? NearestDaysUntilExpiry { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class TenantTseHealthHistoryPointDto
{
    public DateTime CheckedAtUtc { get; set; }
    public Guid? DeviceId { get; set; }
    public string? SerialNumber { get; set; }
    public int HealthScore { get; set; }
    public string HealthStatus { get; set; } = "Unknown";
    public int? ResponseTimeMs { get; set; }
}

public sealed class TenantTseHealthHistoryDto
{
    public Guid TenantId { get; set; }
    public int Days { get; set; }
    public IReadOnlyList<TenantTseHealthHistoryPointDto> Points { get; set; } =
        Array.Empty<TenantTseHealthHistoryPointDto>();
}
