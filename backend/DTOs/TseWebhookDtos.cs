namespace KasseAPI_Final.DTOs;

public sealed class RegisterTseWebhookRequestDto
{
    public Guid TenantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<string> Events { get; set; } = new();
    public string? Secret { get; set; }
}

public sealed class TseWebhookRegistrationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public IReadOnlyList<string> Events { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDeliveryAt { get; set; }
    public bool? LastDeliverySuccess { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool HasSecret { get; set; }
}

public sealed class TseWebhookEventDto
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public object? Payload { get; set; }
}

public sealed class TseWebhookDeliveryResultDto
{
    public Guid DeliveryId { get; set; }
    public Guid WebhookId { get; set; }
    public Guid EventId { get; set; }
    public bool Success { get; set; }
    public int? HttpStatus { get; set; }
    public string? Message { get; set; }
    public DateTime DeliveredAt { get; set; }
}

public sealed class TseWebhookDeliveryLogDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime DeliveredAt { get; set; }
    public bool Success { get; set; }
    public int? HttpStatus { get; set; }
    public string? ResponseSnippet { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class TriggerTseWebhookRequestDto
{
    public string EventType { get; set; } = "Test";
    public object? Payload { get; set; }
}
