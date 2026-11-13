namespace KasseAPI_Final.DTOs;

public sealed class FiskalyErrorQuery
{
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string? OperationType { get; init; }

    public Guid? TenantId { get; init; }

    public string? ReviewStatus { get; init; }

    public string? ErrorCode { get; init; }

    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

public sealed class FiskalyErrorReviewRequest
{
    public string ReviewStatus { get; init; } = string.Empty;
}

public sealed class FiskalyErrorKpisDto
{
    public int TotalErrors { get; init; }

    public double ErrorRatePercent { get; init; }

    public int TotalOperations { get; init; }

    public string? MostCommonErrorCode { get; init; }

    public int MostCommonErrorCount { get; init; }

    public Guid? TenantWithMostErrorsId { get; init; }

    public string? TenantWithMostErrorsName { get; init; }

    public int TenantWithMostErrorsCount { get; init; }

    public int OpenCount { get; init; }

    public int ResolvedCount { get; init; }

    public int KnownIssueCount { get; init; }
}

public sealed class FiskalyErrorCountDto
{
    public string Key { get; init; } = string.Empty;

    public int Count { get; init; }

    public string? SampleMessage { get; init; }
}

public sealed class FiskalyErrorTenantCountDto
{
    public Guid TenantId { get; init; }

    public string? TenantName { get; init; }

    public int Count { get; init; }
}

public sealed class FiskalyErrorDailyPointDto
{
    public string Date { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed class FiskalyErrorWeeklyPointDto
{
    public string Week { get; init; } = string.Empty;

    public string WeekStart { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed class FiskalyKnownErrorSolutionDto
{
    public string Code { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Hint { get; init; } = string.Empty;
}

public class FiskalyErrorListItemDto
{
    public Guid Id { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public string OperationType { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid TenantId { get; init; }

    public string? TenantName { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string? UserDisplayName { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? CashRegisterName { get; init; }

    public string ReviewStatus { get; init; } = string.Empty;

    public DateTime? ReviewedAtUtc { get; init; }

    public string? ReviewedByUserId { get; init; }

    public FiskalyKnownErrorSolutionDto? KnownSolution { get; init; }
}

public sealed class FiskalyErrorDetailDto : FiskalyErrorListItemDto
{
    public string? ReceiptId { get; init; }

    public string? RequestPayloadJson { get; init; }

    public string? ResponsePayloadJson { get; init; }

    public string? StackTrace { get; init; }
}

public sealed class FiskalyErrorStatsDto
{
    public DateTime FromUtc { get; init; }

    public DateTime ToUtc { get; init; }

    public Guid? TenantId { get; init; }

    public FiskalyErrorKpisDto Kpis { get; init; } = new();

    public IReadOnlyList<FiskalyErrorDailyPointDto> Daily { get; init; } = [];

    public IReadOnlyList<FiskalyErrorWeeklyPointDto> Weekly { get; init; } = [];

    public IReadOnlyList<FiskalyErrorCountDto> TopErrors { get; init; } = [];

    public IReadOnlyList<FiskalyErrorCountDto> ByOperationType { get; init; } = [];

    public IReadOnlyList<FiskalyErrorTenantCountDto> ByTenant { get; init; } = [];
}
