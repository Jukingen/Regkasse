using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Canonical status values for <see cref="MaintenanceNotification"/>.</summary>
public static class MaintenanceNotificationStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static bool IsValid(string? status) =>
        status is Draft or Published or InProgress or Completed or Cancelled;

    public static bool IsClientVisible(string? status) =>
        status is Published or InProgress;
}

/// <summary>Which client surfaces a maintenance notice targets.</summary>
public static class MaintenanceAffectedSystems
{
    public const string All = "All";
    public const string Pos = "POS";
    public const string Fa = "FA";
    public const string Api = "API";

    public static bool IsValid(string? value) =>
        value is All or Pos or Fa or Api;

    public static bool AffectsSurface(string? affectedSystems, string surface)
    {
        if (string.IsNullOrWhiteSpace(affectedSystems))
            return true;

        var parts = affectedSystems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return true;

        foreach (var part in parts)
        {
            if (string.Equals(part, All, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(part, surface, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Platform-wide scheduled maintenance notice (not tenant-scoped).
/// Super Admin manages; FA/POS clients read active notices.
/// </summary>
[Table("maintenance_notifications")]
public class MaintenanceNotification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("message", TypeName = "text")]
    public string Message { get; set; } = string.Empty;

    [Column("scheduled_start_at")]
    public DateTime ScheduledStartAt { get; set; }

    [Column("scheduled_end_at")]
    public DateTime ScheduledEndAt { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = MaintenanceNotificationStatuses.Draft;

    /// <summary>1–5 (5 = highest).</summary>
    [Column("priority")]
    public int Priority { get; set; } = 3;

    /// <summary>When true, clients must not allow dismiss.</summary>
    [Column("is_mandatory")]
    public bool IsMandatory { get; set; }

    /// <summary>When true (or within force window), show as non-dismissible overlay.</summary>
    [Column("is_force_display")]
    public bool IsForceDisplay { get; set; }

    /// <summary>UTC instant when force display begins (defaults to 24h before start).</summary>
    [Column("force_display_from")]
    public DateTime? ForceDisplayFrom { get; set; }

    /// <summary>Comma-separated: POS, FA, API, All.</summary>
    [Required]
    [MaxLength(64)]
    [Column("affected_systems")]
    public string AffectedSystems { get; set; } = MaintenanceAffectedSystems.All;

    /// <summary>AspNetUsers Id of the Super Admin who created the notice.</summary>
    [Required]
    [MaxLength(450)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>UTC when the 7-day reminder activity was published (null = not sent).</summary>
    [Column("reminder_7d_sent_at")]
    public DateTime? Reminder7dSentAt { get; set; }

    /// <summary>UTC when the 3-day reminder activity was published.</summary>
    [Column("reminder_3d_sent_at")]
    public DateTime? Reminder3dSentAt { get; set; }

    /// <summary>UTC when the 24-hour reminder (and force-display enable) ran.</summary>
    [Column("reminder_24h_sent_at")]
    public DateTime? Reminder24hSentAt { get; set; }

    /// <summary>UTC when the 1-hour reminder activity was published.</summary>
    [Column("reminder_1h_sent_at")]
    public DateTime? Reminder1hSentAt { get; set; }

    public List<MaintenanceNotificationAcknowledgment> Acknowledgments { get; set; } = new();
}

/// <summary>Per-user read/dismiss state for a maintenance notification.</summary>
[Table("maintenance_notification_acknowledgments")]
public class MaintenanceNotificationAcknowledgment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("notification_id")]
    public Guid NotificationId { get; set; }

    /// <summary>AspNetUsers Id (string Identity key).</summary>
    [Required]
    [MaxLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("is_dismissed")]
    public bool IsDismissed { get; set; }

    [Column("dismissed_at")]
    public DateTime? DismissedAt { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [ForeignKey(nameof(NotificationId))]
    public MaintenanceNotification? Notification { get; set; }
}
