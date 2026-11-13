using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

public sealed class UserUsernameHistoryDto
{
    public Guid Id { get; init; }
    public string? OldUsername { get; init; }
    public required string NewUsername { get; init; }
    public string? ChangedByUserId { get; init; }

    [Obsolete("Not shown in FA UI; use changedByUserId. Planned removal after 2026-12-31.")]
    [JsonPropertyName("changedByEmail")]
    public string? ChangedByEmail { get; init; }

    public DateTime ChangedAtUtc { get; init; }
    public string? Reason { get; init; }
}
