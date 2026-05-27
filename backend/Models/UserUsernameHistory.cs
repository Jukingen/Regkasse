using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Append-only log of login username changes for compliance and support.</summary>
[Table("user_username_history")]
public class UserUsernameHistory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("old_username")]
    public string? OldUsername { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("new_username")]
    public string NewUsername { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("changed_by_user_id")]
    public string? ChangedByUserId { get; set; }

    [Column("changed_at_utc")]
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }

    [ForeignKey(nameof(ChangedByUserId))]
    public virtual ApplicationUser? ChangedByUser { get; set; }
}
