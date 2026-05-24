using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>PUT /api/admin/cash-registers/{id} — update stammdaten for a tenant-scoped register row.</summary>
public sealed class UpdateCashRegisterRequest
{
    [Required]
    [MaxLength(20)]
    public string RegisterNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;
}
