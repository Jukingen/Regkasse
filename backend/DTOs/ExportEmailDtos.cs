using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class SendExportEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string To { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Message { get; set; }

    /// <summary>When set in the future (UTC), queue for scheduled delivery instead of sending now.</summary>
    public DateTime? ScheduledForUtc { get; set; }

    [MaxLength(64)]
    public string? SourceKind { get; set; }

    public Guid? SourceId { get; set; }

    /// <summary>Force link delivery even when under attachment size cap.</summary>
    public bool PreferLink { get; set; }
}

public sealed class SendExportEmailResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DeliveryMode { get; set; } = string.Empty;
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Message { get; set; }
}

public sealed class ExportEmailDeliveryListItemDto
{
    public Guid Id { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DeliveryMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime? ScheduledForUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ExportEmailDeliveryListResponse
{
    public IReadOnlyList<ExportEmailDeliveryListItemDto> Items { get; set; } =
        Array.Empty<ExportEmailDeliveryListItemDto>();

    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class CancelExportEmailScheduleResponse
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}
