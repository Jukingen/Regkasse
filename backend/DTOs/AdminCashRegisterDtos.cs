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
    public bool IsActive { get; init; }
    public DateTime? DecommissionedAtUtc { get; init; }
    public string? DecommissionReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}
