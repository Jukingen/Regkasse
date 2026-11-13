namespace KasseAPI_Final.DTOs;

/// <summary>TSE device row for Super Admin failover dashboard.</summary>
public sealed class TseFailoverDeviceDto
{
    public Guid Id { get; init; }

    public string? DeviceId { get; init; }

    public string SerialNumber { get; init; } = string.Empty;

    public string? Provider { get; init; }

    public string DeviceType { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public string? TenantSlug { get; init; }

    public Guid? CashRegisterId { get; init; }

    public string? CashRegisterNumber { get; init; }

    public bool IsPrimary { get; init; }

    public bool IsBackup { get; init; }

    public bool IsActive { get; init; }

    public bool IsFailoverActive { get; init; }

    public Guid? PrimaryDeviceId { get; init; }

    public string HealthStatus { get; init; } = "Healthy";

    public int HealthScore { get; init; }

    public string? HealthMessage { get; init; }

    public DateTime? LastHealthCheck { get; init; }

    public int FailoverCount { get; init; }

    public DateTime? LastFailoverAt { get; init; }

    public string? LastFailoverReason { get; init; }
}

/// <summary>One currently active primary→backup failover pairing.</summary>
public sealed class TseActiveFailoverDto
{
    public Guid Id { get; init; }

    public Guid PrimaryDeviceId { get; init; }

    public string? PrimarySerialNumber { get; init; }

    public Guid BackupDeviceId { get; init; }

    public string? BackupSerialNumber { get; init; }

    public Guid? TenantId { get; init; }

    public string? TenantName { get; init; }

    public DateTime? LastFailoverAt { get; init; }

    public string? LastFailoverReason { get; init; }
}

public sealed class TseFailoverStatusDto
{
    public int ActiveFailoverCount { get; init; }

    public int HealthyDeviceCount { get; init; }

    public int ActiveDeviceCount { get; init; }

    public int BackupAvailableCount { get; init; }

    public bool AutoFailoverEnabled { get; init; }

    public IReadOnlyList<TseActiveFailoverDto> ActiveFailovers { get; init; } =
        Array.Empty<TseActiveFailoverDto>();
}

public sealed class TseFailoverHistoryItemDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid PrimaryDeviceId { get; init; }

    public Guid? BackupDeviceId { get; init; }

    public string FailoverType { get; init; } = string.Empty;

    public string TriggerReason { get; init; } = string.Empty;

    public string? PreviousStatus { get; init; }

    public string? NewStatus { get; init; }

    public bool IsSuccessful { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public string? PerformedBy { get; init; }

    public string? Notes { get; init; }
}

public sealed class ManualTseFailoverRequestDto
{
    public Guid PrimaryDeviceId { get; set; }

    public Guid BackupDeviceId { get; set; }
}

public sealed class RevertTseFailoverRequestDto
{
    public Guid PrimaryDeviceId { get; set; }
}

public sealed class TseFailoverActionResponseDto
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? FailoverType { get; init; }

    public Guid? PrimaryDeviceId { get; init; }

    public Guid? BackupDeviceId { get; init; }

    public Guid? LogId { get; init; }

    public bool NeedsAttention { get; init; }
}
