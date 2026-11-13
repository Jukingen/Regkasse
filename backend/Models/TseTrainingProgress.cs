using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin TSE training module progress (per user). Not fiscal.
/// </summary>
[Table("tse_training_progress")]
public sealed class TseTrainingProgress
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("module_id")]
    public string ModuleId { get; set; } = string.Empty;

    [Column("is_started")]
    public bool IsStarted { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
