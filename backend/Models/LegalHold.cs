using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Sprint 5: Legal hold on audit records. When active, cleanup must not delete audit logs whose date falls within [FromDate, ToDate].
/// </summary>
[Table("legal_holds")]
public class LegalHold
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("from_date", TypeName = "date")]
    public DateTime FromDate { get; set; }

    [Required]
    [Column("to_date", TypeName = "date")]
    public DateTime ToDate { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }
}
