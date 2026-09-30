using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Activity feed item returned by <c>GET /api/admin/activities</c>.</summary>
public sealed class ActivityDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Event kind. JSON is the <see cref="ActivityEventType"/> member name
    /// (for example <c>UserCreated</c>). The converter stays on this property so
    /// duplicate underlying values do not collapse notification-config dictionary keys.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ActivityEventType Type { get; set; }

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
