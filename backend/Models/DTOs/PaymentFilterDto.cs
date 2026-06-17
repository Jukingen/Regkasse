namespace KasseAPI_Final.Models.DTOs;

/// <summary>Advanced query filters for admin payment list.</summary>
public class PaymentFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public List<string> PaymentMethods { get; set; } = new();
    public List<string> Statuses { get; set; } = new();

    public Guid? CashRegisterId { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }

    public string? CashierId { get; set; }
    public string? ReceiptNumber { get; set; }

    public bool? IsStorno { get; set; }
    public bool? IsRefund { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SortBy { get; set; } = "CreatedAt";
    public string? SortDirection { get; set; } = "desc";

    /// <summary>Opaque keyset cursor from a prior page's <see cref="PaymentListResponse.NextCursor"/>.</summary>
    public string? AfterCursor { get; set; }

    /// <summary>When false, skips the expensive COUNT query (deep keyset pages).</summary>
    public bool IncludeTotalCount { get; set; } = true;
}

public class PaymentListResponse
{
    public List<PaymentListItemDto> Items { get; set; } = new();
    public int? TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
    public FilterSummaryDto ActiveFilters { get; set; } = new();
}

public class FilterSummaryDto
{
    public int ActiveFilterCount { get; set; }
    public Dictionary<string, object> AppliedFilters { get; set; } = new();
    public List<string> AvailablePaymentMethods { get; set; } = new();
    public List<string> AvailableStatuses { get; set; } = new();
}
