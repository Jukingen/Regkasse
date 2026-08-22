using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class AdminCashRegisterCapabilitiesDto
{
    public bool AllowHardDelete { get; set; }

    /// <summary>True when decommission uses RKSV Schlussbeleg (production-safe path).</summary>
    public bool DecommissionViaSchlussbeleg { get; set; } = true;
}

public sealed class DecommissionCashRegisterRequest
{
    [MaxLength(450)]
    public string? Reason { get; set; }
}

public sealed class DecommissionCashRegisterResponse
{
    public Guid CashRegisterId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class HardDeleteCashRegisterRequest
{
    /// <summary>Must be exactly <c>HARD_DELETE</c> when <see cref="Configuration.CashRegisterComplianceOptions.AllowHardDelete"/> is enabled.</summary>
    [Required]
    public string ConfirmPhrase { get; set; } = string.Empty;
}

/// <summary>POS device metadata last observed for a register (nullable until POS reports it).</summary>
public sealed class CashRegisterDeviceInfoDto
{
    public string? Model { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
}

/// <summary>Per-register TSE health snapshot for admin FA.</summary>
public sealed class CashRegisterTseHealthDto
{
    /// <summary>healthy | degraded | offline | notConfigured</summary>
    public string Status { get; set; } = "notConfigured";

    public DateTime? LastCheckUtc { get; set; }

    public string? Message { get; set; }

    public int OfflineQueueCount { get; set; }
}

/// <summary>Admin FA list/detail projection for cash register inventory rows.</summary>
public sealed class CashRegisterDto
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

    /// <summary>
    /// Admin-managed cashier assignment. Null means the register is selectable by every POS user of the tenant.
    /// Controls POS picker visibility only — payment authorization stays on <see cref="CurrentUserId"/> (open shift).
    /// </summary>
    public string? AssignedUserId { get; init; }

    public bool IsActive { get; init; }
    public bool IsDefaultForTenant { get; init; }
    public DateTime? DecommissionedAtUtc { get; init; }
    public string? DecommissionReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }

    public DateTime? LastMonatsbelegUtc { get; set; }
    public DateTime? LastJahresbelegUtc { get; set; }

    /// <summary>UTC timestamp when the RKSV Startbeleg was recorded on this register; null when not yet created.</summary>
    public DateTime? StartbelegCreatedAtUtc { get; set; }

    /// <summary>healthy | degraded | offline | notConfigured</summary>
    public string TseHealthStatus { get; set; } = "notConfigured";

    public int OfflineQueueCount { get; set; }

    /// <summary>Last successful fiscal payment or TSE health ping (UTC).</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    public string? CurrentCashierName { get; set; }

    /// <summary>Login of the open-shift cashier; used to disambiguate similar display names.</summary>
    public string? CurrentCashierUserName { get; set; }

    /// <summary>Email of the open-shift cashier; omitted when the till is unattended.</summary>
    public string? CurrentCashierEmail { get; set; }

    /// <summary>Display name of <see cref="AssignedUserId"/>; null when the register is unassigned.</summary>
    public string? AssignedUserName { get; set; }

    public CashRegisterDeviceInfoDto DeviceInfo { get; set; } = new();
}

/// <summary>
/// Assigns a cash register to a cashier, or clears the assignment when <see cref="UserId"/> is null or empty.
/// </summary>
public sealed class AssignCashRegisterUserRequest
{
    /// <summary>Target user id. Must be an active member of the register's tenant. Null or empty removes the assignment.</summary>
    public string? UserId { get; init; }

    /// <summary>
    /// SuperAdmin-only: skip tenant assignment cap (<c>maxActiveRegistersPerUser</c>).
    /// Ignored for every other role.
    /// </summary>
    public bool Force { get; init; }
}
