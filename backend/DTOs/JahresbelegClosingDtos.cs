using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.DTOs;

public sealed class CreateJahresbelegClosingRequest
{
    public Guid CashRegisterId { get; set; }

    /// <summary>When omitted, uses the current Vienna calendar year.</summary>
    public int? Year { get; set; }
}

public sealed class JahresbelegClosingResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid? JahresbelegId { get; init; }

    public Guid? DailyClosingId { get; init; }

    public int Year { get; init; }

    public decimal TotalGross { get; init; }

    public decimal TotalTax { get; init; }

    public int TransactionCount { get; init; }

    public int MonatsbelegCount { get; init; }

    public string? TseSignature { get; init; }

    public string? PreviousSignature { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = "Production";

    public bool IsDecemberMonatsbeleg { get; init; }

    public PosDailyClosingReportDto? Report { get; init; }
}

public sealed class JahresbelegListItemDto
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? RegisterNumber { get; init; }

    public int Year { get; init; }

    public decimal TotalGross { get; init; }

    public int TransactionCount { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public bool IsDecemberMonatsbeleg { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}

public sealed class JahresbelegDetailDto
{
    public Guid Id { get; init; }

    public Guid CashRegisterId { get; init; }

    public string? RegisterNumber { get; init; }

    public int Year { get; init; }

    public JahresbelegSummaryDto Summary { get; init; } = new();

    public string? TseSignature { get; init; }

    public string? PreviousSignature { get; init; }

    public int SignatureChainLength { get; init; }

    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = string.Empty;

    public bool IsDecemberMonatsbeleg { get; init; }

    public string RksvFooterLabel { get; init; } = string.Empty;

    public string TseStatusBadge { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public Guid? DailyClosingId { get; init; }
}
