using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Activity feed item returned by <c>GET /api/admin/activities</c>.</summary>
public sealed class ActivityDto
{
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    public string Severity { get; set; } = ActivitySeverityNames.Info;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ActorUserId { get; set; }

    public string? ActorName { get; set; }

    public string? EntityId { get; set; }

    public string? EntityType { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}

public sealed class ActivitiesListResponseDto
{
    public IReadOnlyList<ActivityDto> Items { get; set; } = [];

    public int Total { get; set; }

    public int Limit { get; set; }

    public int Offset { get; set; }
}

public sealed class ActivitiesUnreadCountDto
{
    public int UnreadCount { get; set; }
}
