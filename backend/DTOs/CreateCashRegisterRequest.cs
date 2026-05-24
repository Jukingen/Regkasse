using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>POST /api/CashRegister — create a closed register row for the effective or selected tenant.</summary>
public sealed class CreateCashRegisterRequest
{
    /// <summary>Register number unique within the tenant (e.g. KASSE-001).</summary>
    [Required]
    [MaxLength(20)]
    public string RegisterNumber { get; set; } = string.Empty;

    /// <summary>Human-readable location label (e.g. Hauptkasse, Theke).</summary>
    [Required]
    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Optional target tenant; only SuperAdmin may set this. Managers use the current tenant context.</summary>
    public Guid? TenantId { get; set; }
}
