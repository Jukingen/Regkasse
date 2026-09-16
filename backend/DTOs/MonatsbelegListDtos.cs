namespace KasseAPI_Final.DTOs;

public sealed class MonatsbelegListQuery
{
    public int? Year { get; init; }

    public Guid? CashRegisterId { get; init; }

    /// <summary><c>all</c> | <c>created</c> | <c>failed</c> | <c>fonPending</c>.</summary>
    public string? Status { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class MonatsbelegListResponse
{
    public int Year { get; init; }

    public int Total { get; init; }

    public bool HasFailedAutoCreates { get; init; }

    public IReadOnlyList<MonatsbelegListRowDto> Items { get; init; } = [];
}

public sealed class MonatsbelegListRowDto
{
    public Guid? PaymentId { get; init; }

    public Guid CashRegisterId { get; init; }

    public string RegisterNumber { get; init; } = string.Empty;

    public string? RegisterLocation { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public string Period { get; init; } = string.Empty;

    public DateTime? CreatedAtUtc { get; init; }

    public string CreatedBy { get; init; } = string.Empty;

    public string CreatedByUserId { get; init; } = string.Empty;

    public string TseSignature { get; init; } = string.Empty;

    public string DepStatus { get; init; } = "Missing";

    public string FonStatus { get; init; } = "NotRequired";

    public bool IsJahresbeleg { get; init; }

    public bool AutoCreated { get; init; }

    /// <summary><c>created</c> | <c>failed</c>.</summary>
    public string Status { get; init; } = "created";

    public string? LastError { get; init; }

    public int AttemptCount { get; init; }

    public string? CorrelationId { get; init; }
}
