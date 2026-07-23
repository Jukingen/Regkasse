namespace KasseAPI_Final.DTOs;

public sealed class TseHealthReportDto
{
    public Guid TenantId { get; set; }
    public string? TenantName { get; set; }
    public string? TenantSlug { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int TotalDevices { get; set; }
    public int HealthyDevices { get; set; }
    public int DegradedDevices { get; set; }
    public int UnhealthyDevices { get; set; }
    public double AverageHealthScore { get; set; }
    public double MinHealthScore { get; set; }
    public double MaxHealthScore { get; set; }
    public int HealthyMinScore { get; set; }
    public int DegradedMinScore { get; set; }
    public IReadOnlyList<TseDeviceHealthSummaryDto> DeviceSummaries { get; set; } =
        Array.Empty<TseDeviceHealthSummaryDto>();
    public IReadOnlyList<TseHealthAlertDto> RecentAlerts { get; set; } = Array.Empty<TseHealthAlertDto>();
    public IReadOnlyList<TseHealthRecommendationDto> Recommendations { get; set; } =
        Array.Empty<TseHealthRecommendationDto>();
}

public sealed class TseDeviceHealthSummaryDto
{
    public Guid DeviceId { get; set; }
    public string? VendorDeviceId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsBackup { get; set; }
    public bool IsFailoverActive { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
    public int HealthScore { get; set; }
    public string? HealthMessage { get; set; }
    public DateTime? LastHealthCheck { get; set; }
}

public sealed class TseHealthAlertDto
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "Activity";
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime AtUtc { get; set; }
}

public sealed class TseHealthRecommendationDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
}

public sealed class TseHealthTrendPointDto
{
    public DateTime Date { get; set; }
    public Guid DeviceId { get; set; }
    public string? DeviceLabel { get; set; }
    public int Score { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
}
