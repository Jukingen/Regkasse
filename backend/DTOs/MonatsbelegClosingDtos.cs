using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.DTOs;

public sealed class CreateMonatsbelegClosingRequest
{
    public Guid CashRegisterId { get; set; }

    /// <summary>When omitted, uses the current Vienna calendar month.</summary>
    public int? Year { get; set; }

    /// <summary>When omitted, uses the current Vienna calendar month (1–12).</summary>
    public int? Month { get; set; }
}

public sealed class MonatsbelegClosingResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid? MonatsbelegId { get; init; }

    public Guid? DailyClosingId { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public decimal TotalGross { get; init; }

    public decimal TotalTax { get; init; }

    public int TransactionCount { get; init; }

    public int DailyClosingCount { get; init; }

    public string? TseSignature { get; init; }

    public string? PreviousSignature { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = "Production";

    public PosDailyClosingReportDto? Report { get; init; }
}

public sealed class MonatsbelegListItemDto
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? RegisterNumber { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public decimal TotalGross { get; init; }

    public int TransactionCount { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}

public sealed class MonatsbelegDetailDto
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? RegisterNumber { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public MonatsbelegSummaryDto Summary { get; init; } = new();

    public string? TseSignature { get; init; }

    public string? PreviousSignature { get; init; }

    public int SignatureChainLength { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public string RksvFooterLabel { get; init; } = string.Empty;

    public string TseStatusBadge { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public Guid? DailyClosingId { get; init; }
}
