namespace KasseAPI_Final.Services.Support;

public sealed record CreateSupportTicketRequest
{
    public string Category { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    /// <summary>Preferred subject field. <see cref="Title"/> is accepted as an alias.</summary>
    public string Subject { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public string ResolvedSubject()
    {
        var subject = (Subject ?? string.Empty).Trim();
        if (subject.Length > 0)
            return subject;
        return (Title ?? string.Empty).Trim();
    }
}

public sealed record AddSupportTicketMessageRequest
{
    public string Body { get; init; } = string.Empty;
    public bool IsInternal { get; init; }
}

public sealed record UpdateSupportTicketStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed record AssignSupportTicketRequest
{
    public string AssignedToUserId { get; init; } = string.Empty;
}

public sealed record SupportTicketMessageDto
{
    public Guid Id { get; init; }
    public string AuthorUserId { get; init; } = string.Empty;
    public string? AuthorDisplayName { get; init; }
    public string Body { get; init; } = string.Empty;
    public bool IsStaffReply { get; init; }
    public bool IsInternal { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record SupportTicketListItemDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string TicketNumber { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subject => Title;
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? CreatedByDisplayName { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? AssignedToDisplayName { get; init; }
    public DateTime? ResolvedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public int MessageCount { get; init; }
}

public sealed record SupportTicketDetailDto : SupportTicketListItemDto
{
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<SupportTicketMessageDto> Messages { get; init; } = [];
}

public sealed record SupportTicketListResponse
{
    public IReadOnlyList<SupportTicketListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalPages { get; init; }
    public int OpenCount { get; init; }
}

public sealed record SupportTicketInboxSummaryDto
{
    public int OpenCount { get; init; }
    public int InProgressCount { get; init; }
    public int ResolvedCount { get; init; }
    public int ResolvedLast30DaysCount { get; init; }
    public int ClosedCount { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyDictionary<string, int> ByCategory { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ByPriority { get; init; } =
        new Dictionary<string, int>();
}

public sealed record SupportTicketListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Priority { get; init; }
    public string? Search { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
}
