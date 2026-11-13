using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class CreateMaintenanceNotificationRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledStartAt { get; set; }

    [Required]
    public DateTime ScheduledEndAt { get; set; }

    [Range(1, 5)]
    public int Priority { get; set; } = 3;

    public bool IsMandatory { get; set; }

    public bool IsForceDisplay { get; set; }

    public DateTime? ForceDisplayFrom { get; set; }

    /// <summary>POS, FA, API, All — or comma-separated combination.</summary>
    [Required]
    [MaxLength(64)]
    public string AffectedSystems { get; set; } = "All";

    /// <summary>When true, create already Published.</summary>
    public bool PublishImmediately { get; set; }
}

public sealed class UpdateMaintenanceNotificationRequestDto
{
    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(4000)]
    public string? Message { get; set; }

    public DateTime? ScheduledStartAt { get; set; }

    public DateTime? ScheduledEndAt { get; set; }

    [Range(1, 5)]
    public int? Priority { get; set; }

    public bool? IsMandatory { get; set; }

    public bool? IsForceDisplay { get; set; }

    public DateTime? ForceDisplayFrom { get; set; }

    [MaxLength(64)]
    public string? AffectedSystems { get; set; }
}

public sealed class AcknowledgeMaintenanceNotificationRequestDto
{
    /// <summary>Mark as dismissed (ignored when notice is mandatory / force-display).</summary>
    public bool Dismiss { get; set; } = true;

    /// <summary>Mark as read.</summary>
    public bool MarkRead { get; set; } = true;
}

public sealed class MaintenanceNotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ScheduledStartAt { get; set; }
    public DateTime ScheduledEndAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsMandatory { get; set; }
    public bool IsForceDisplay { get; set; }
    public DateTime? ForceDisplayFrom { get; set; }
    public string AffectedSystems { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Effective force overlay for the current user/time (24h window or flags).</summary>
    public bool EffectiveForceDisplay { get; set; }

    /// <summary>Whether the current user may dismiss this notice.</summary>
    public bool CanDismiss { get; set; }

    public bool IsDismissedByCurrentUser { get; set; }
    public bool IsReadByCurrentUser { get; set; }
}

public sealed class MaintenanceNotificationListResponseDto
{
    public IReadOnlyList<MaintenanceNotificationDto> Items { get; set; } = Array.Empty<MaintenanceNotificationDto>();
    public int Total { get; set; }
}
