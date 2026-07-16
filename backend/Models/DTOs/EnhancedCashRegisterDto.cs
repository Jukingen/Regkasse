using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Admin / back-office cash register row with operational telemetry.</summary>
public sealed class EnhancedCashRegisterDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string RegisterNumber { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public RegisterStatus Status { get; init; }
    public decimal StartingBalance { get; init; }
    public decimal CurrentBalance { get; init; }
    public DateTime LastBalanceUpdate { get; init; }
    public string? CurrentUserId { get; init; }
    public bool IsActive { get; init; }
    public DateTime? DecommissionedAtUtc { get; init; }
    public string? DecommissionReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }

    public DateTime? LastMonatsbelegUtc { get; set; }
    public DateTime? LastJahresbelegUtc { get; set; }

    /// <summary>UTC timestamp when the RKSV Startbeleg was recorded; null when not yet created.</summary>
    public DateTime? StartbelegCreatedAtUtc { get; set; }

    /// <summary>healthy | degraded | offline | notConfigured</summary>
    public string TseHealthStatus { get; set; } = "notConfigured";

    public int OfflineQueueCount { get; set; }

    public DateTime? LastSyncAtUtc { get; set; }

    public string? CurrentCashierName { get; set; }

    public CashRegisterDeviceInfoDto DeviceInfo { get; set; } = new();

    public static EnhancedCashRegisterDto From(CashRegisterDto source) =>
        new()
        {
            Id = source.Id,
            TenantId = source.TenantId,
            TenantName = source.TenantName,
            TenantSlug = source.TenantSlug,
            RegisterNumber = source.RegisterNumber,
            Location = source.Location,
            Status = source.Status,
            StartingBalance = source.StartingBalance,
            CurrentBalance = source.CurrentBalance,
            LastBalanceUpdate = source.LastBalanceUpdate,
            CurrentUserId = source.CurrentUserId,
            IsActive = source.IsActive,
            DecommissionedAtUtc = source.DecommissionedAtUtc,
            DecommissionReason = source.DecommissionReason,
            CreatedAt = source.CreatedAt,
            CreatedBy = source.CreatedBy,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
            LastMonatsbelegUtc = source.LastMonatsbelegUtc,
            LastJahresbelegUtc = source.LastJahresbelegUtc,
            StartbelegCreatedAtUtc = source.StartbelegCreatedAtUtc,
            TseHealthStatus = source.TseHealthStatus,
            OfflineQueueCount = source.OfflineQueueCount,
            LastSyncAtUtc = source.LastSyncAtUtc,
            CurrentCashierName = source.CurrentCashierName,
            DeviceInfo = source.DeviceInfo,
        };
}
