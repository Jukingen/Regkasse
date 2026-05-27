using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

public sealed record ActivityEventPublishRequest(
    Guid TenantId,
    ActivityEventType Type,
    string Title,
    string? Description = null,
    string? Severity = null,
    string? DedupKey = null,
    string? ActorUserId = null,
    string? ActorName = null,
    string? EntityType = null,
    string? EntityId = null,
    IReadOnlyDictionary<string, object>? Metadata = null,
    bool SkipOutboundDelivery = false);
