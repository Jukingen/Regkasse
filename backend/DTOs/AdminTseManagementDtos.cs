namespace KasseAPI_Final.DTOs;

/// <summary>One TSE device row enriched for Super Admin fleet management.</summary>
public sealed class TseDeviceFleetItemDto
{
    public Guid Id { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public string DeviceType { get; init; } = string.Empty;

    public Guid CashRegisterId { get; init; }

    public string? CashRegisterNumber { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public string? TenantSlug { get; init; }

    /// <summary>Active | Degraded | Inactive | Expired</summary>
    public string Status { get; init; } = "Inactive";

    public bool IsConnected { get; init; }

    public bool CanCreateInvoices { get; init; }

    public bool IsActive { get; init; }

    public string CertificateStatus { get; init; } = "UNKNOWN";

    public string MemoryStatus { get; init; } = "UNKNOWN";

    public string? ErrorMessage { get; init; }

    /// <summary>Derived 0–100 score from connection / cert / active flags (not stored).</summary>
    public int HealthScore { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastConnectionTime { get; init; }

    public DateTime LastSignatureTime { get; init; }
}

/// <summary>Super Admin TSE fleet dashboard overview.</summary>
public sealed class TseFleetOverviewDto
{
    public int TotalDevices { get; init; }

    public int ActiveDevices { get; init; }

    public int DegradedDevices { get; init; }

    public int InactiveDevices { get; init; }

    public int ExpiredCertificateDevices { get; init; }

    /// <summary>Process-wide probe health score 0–100 from <c>ITseHealthMonitor</c>.</summary>
    public int ProcessHealthScore { get; init; }

    public string ProcessHealthStatus { get; init; } = "Degraded";

    public DateTime? ProcessLastCheckUtc { get; init; }

    public string? ProcessLastErrorSafe { get; init; }

    public string TseMode { get; init; } = string.Empty;

    public string SigningMode { get; init; } = string.Empty;

    public IReadOnlyList<TseDeviceFleetItemDto> Devices { get; init; } = Array.Empty<TseDeviceFleetItemDto>();
}

public sealed class ProvisionTseRequestDto
{
    /// <summary>Provision for the tenant's default cash register.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>When set, takes precedence over <see cref="TenantId"/>.</summary>
    public Guid? CashRegisterId { get; set; }
}

public sealed class ProvisionTseResponseDto
{
    public bool Success { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? Error { get; init; }

    public string? Detail { get; init; }

    public Guid? DeviceId { get; init; }

    public string? SerialNumber { get; init; }

    public Guid? CashRegisterId { get; init; }
}
