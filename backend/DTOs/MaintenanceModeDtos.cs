using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>Platform-wide maintenance mode status (derived from InProgress / in-window notices).</summary>
public sealed class MaintenanceModeStatusDto
{
    public bool IsActive { get; set; }
    public Guid? NotificationId { get; set; }
    public string? Title { get; set; }
    public string? Message { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public string Status { get; set; } = "Inactive";
    public bool BlocksPosPayments { get; set; }
    public bool BlocksApiWrites { get; set; }
}

public sealed class StartMaintenanceModeRequestDto
{
    /// <summary>UTC end of the window. Defaults to now + 2 hours when omitted.</summary>
    public DateTime? ScheduledEndAt { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(4000)]
    public string? Message { get; set; }

    [Range(1, 5)]
    public int Priority { get; set; } = 5;

    public bool IsMandatory { get; set; } = true;
}
