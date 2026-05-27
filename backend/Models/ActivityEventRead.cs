using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Per-user read state for an activity event.</summary>
[Table("activity_event_reads")]
public class ActivityEventRead
{
    [Column("activity_event_id")]
    public Guid ActivityEventId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("read_at_utc")]
    public DateTime ReadAtUtc { get; set; }

    [ForeignKey(nameof(ActivityEventId))]
    public ActivityEvent? ActivityEvent { get; set; }
}
