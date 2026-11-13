using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class TaxGroupAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSystem { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public string? GroupType { get; set; }
    public string? AustrianCode { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public Guid? ReplacedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class UpsertTaxGroupRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 100)]
    public decimal Rate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    [MaxLength(8)]
    public string? AustrianCode { get; set; }

    public DateTime? ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }
}
