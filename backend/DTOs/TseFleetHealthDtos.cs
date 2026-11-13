namespace KasseAPI_Final.DTOs;

/// <summary>Single device row in the Super Admin fleet health status response.</summary>
public sealed class TseFleetDeviceHealthDto
{
    public Guid DeviceId { get; set; }
    public Guid? TenantId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Provider { get; set; }
    public string Status { get; set; } = "Unknown";
    public int HealthScore { get; set; }
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? LastHealthCheckUtc { get; set; }
    public int? ResponseTimeMs { get; set; }
}

/// <summary>Overall TSE fleet health for external monitors / FA ops.</summary>
public sealed class TseFleetHealthStatusDto
{
    /// <summary><c>healthy</c> | <c>degraded</c> | <c>unhealthy</c></summary>
    public string Status { get; set; } = "degraded";

    public DateTime CheckedAtUtc { get; set; }
    public bool LiveProbe { get; set; }
    public int DeviceCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public IReadOnlyList<TseFleetDeviceHealthDto> Devices { get; set; } =
        Array.Empty<TseFleetDeviceHealthDto>();
    public TseHealthMetricsSummaryDto Metrics { get; set; } = new();
}

/// <summary>JSON summary gauges for TSE fleet monitoring.</summary>
public sealed class TseHealthMetricsSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int ActiveDevices { get; set; }
    public int HealthyDevices { get; set; }
    public int DegradedDevices { get; set; }
    public int UnhealthyDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int ExpiredOrRevokedDevices { get; set; }
    public double AverageHealthScore { get; set; }
    public int ActiveFailoverCount { get; set; }
    public int PrimaryDevices { get; set; }
    public int BackupDevices { get; set; }
    public double? MaxStalenessSeconds { get; set; }
    public DateTime? OldestHealthCheckUtc { get; set; }
    public IReadOnlyDictionary<string, int> DevicesByProvider { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> DevicesByStatus { get; set; } =
        new Dictionary<string, int>();
}
