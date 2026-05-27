using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services.Activity;

internal static class ActivityEventMapper
{
    public static ActivityDto ToDto(ActivityEvent e, bool isRead = false, DateTime? readAtUtc = null) =>
        new()
        {
            Id = e.Id,
            Type = e.Type.ToString(),
            Severity = e.Severity,
            Title = e.Title,
            Description = e.Description,
            ActorUserId = e.ActorUserId,
            ActorName = e.ActorName,
            EntityId = e.EntityId,
            EntityType = e.EntityType,
            Metadata = DeserializeMetadata(e.MetadataJson),
            IsRead = isRead,
            CreatedAtUtc = e.CreatedAtUtc,
            ReadAtUtc = readAtUtc,
        };

    private static Dictionary<string, object>? DeserializeMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }
}
