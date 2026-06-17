namespace KasseAPI_Final.Models.DTOs;

/// <summary>OpenAPI-stable admin payments list envelope (backward-compatible property names).</summary>
public class AdminPaymentsListResponse
{
    public int Total { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<AdminPaymentListItemDto> Items { get; set; } = Array.Empty<AdminPaymentListItemDto>();
    public FilterSummaryDto? ActiveFilters { get; set; }
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }

    public static AdminPaymentsListResponse From(PaymentListResponse response) => new()
    {
        Total = response.TotalCount ?? 0,
        PageNumber = response.Page,
        PageSize = response.PageSize,
        Items = response.Items.Cast<AdminPaymentListItemDto>().ToList(),
        ActiveFilters = response.ActiveFilters,
        NextCursor = response.NextCursor,
        HasMore = response.HasMore,
    };
}
