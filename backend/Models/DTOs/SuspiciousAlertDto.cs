using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models.DTOs;

/// <summary>Suspicious payment alert returned by <c>GET /api/admin/payments/alerts</c>.</summary>
public sealed class SuspiciousAlertDto
{
    public Guid Id { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SuspiciousAlertType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SuspiciousAlertSeverity Severity { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SuspiciousAlertStatus Status { get; set; }

    public Guid? PaymentId { get; set; }

    public Guid? CustomerId { get; set; }

    public string? UserId { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? SuggestedAction { get; set; }

    public Dictionary<string, object>? Details { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class SuspiciousAlertsListResponseDto
{
    public IReadOnlyList<SuspiciousAlertDto> Items { get; set; } = [];

    public int Total { get; set; }
}
