namespace KasseAPI_Final.Models.DTOs;

/// <summary>OpenAPI-stable admin payments list envelope (backward-compatible property names).</summary>
public class AdminPaymentsListResponse
{
    public int Total { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<AdminPaymentListItemDto> Items { get; set; } = Array.Empty<AdminPaymentListItemDto>();
    public FilterSummaryDto? ActiveFilters { get; set; }

    public static AdminPaymentsListResponse From(PaymentListResponse response) => new()
    {
        Total = response.TotalCount,
        PageNumber = response.Page,
        PageSize = response.PageSize,
        Items = response.Items.Cast<AdminPaymentListItemDto>().ToList(),
        ActiveFilters = response.ActiveFilters,
    };
}
