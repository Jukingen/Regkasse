using System.ComponentModel.DataAnnotations;

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
