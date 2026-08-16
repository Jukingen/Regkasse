namespace KasseAPI_Final.Services.Billing;

/// <summary>Tenant-facing invoice payment status (mapped from <c>license_sales.status</c>).</summary>
public static class TenantInvoiceStatuses
{
    public const string Paid = "paid";
    public const string Unpaid = "unpaid";
    public const string Overdue = "overdue";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";

    public static string FromLicenseSaleStatus(string? saleStatus)
    {
        if (string.Equals(saleStatus, Models.LicenseSaleStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            return Cancelled;
        if (string.Equals(saleStatus, Models.LicenseSaleStatuses.Refunded, StringComparison.OrdinalIgnoreCase))
            return Refunded;
        return Paid;
    }

    /// <summary>
    /// Maps a tenant filter (Paid/Unpaid/Overdue or license-sale aliases) to a sale status.
    /// Unpaid/Overdue have no license-sale rows (sales are recorded as paid).
    /// Returns <see langword="null"/> when the filter should not restrict rows.
    /// </summary>
    public static bool TryMapFilter(string? status, out string? licenseSaleStatus, out bool matchNone)
    {
        licenseSaleStatus = null;
        matchNone = false;

        if (string.IsNullOrWhiteSpace(status)
            || string.Equals(status.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var key = status.Trim().ToLowerInvariant();
        switch (key)
        {
            case Paid:
            case Models.LicenseSaleStatuses.Active:
                licenseSaleStatus = Models.LicenseSaleStatuses.Active;
                return true;
            case Cancelled:
                licenseSaleStatus = Models.LicenseSaleStatuses.Cancelled;
                return true;
            case Refunded:
                licenseSaleStatus = Models.LicenseSaleStatuses.Refunded;
                return true;
            case Unpaid:
            case Overdue:
                matchNone = true;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>Mandanten-Admin self-service license invoice (mapped from <c>license_sales</c>).</summary>
public sealed record TenantInvoiceDto
{
    public Guid Id { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    /// <summary>Sale timestamp (UTC). Same instant as <see cref="InvoiceDateUtc"/>.</summary>
    public DateTime IssuedAt { get; init; }
    /// <summary>Alias of <see cref="IssuedAt"/> for existing FA clients.</summary>
    public DateTime InvoiceDateUtc { get; init; }
    public decimal AmountNet { get; init; }
    public decimal VatAmount { get; init; }
    public decimal AmountGross { get; init; }
    public string Currency { get; init; } = "EUR";
    /// <summary><c>paid</c>, <c>unpaid</c>, <c>overdue</c>, <c>cancelled</c>, or <c>refunded</c>.</summary>
    public string Status { get; init; } = string.Empty;
    public string? LicenseKey { get; init; }
    public string? LicensePlan { get; init; }
    /// <summary>Relative API path for PDF download.</summary>
    public string DownloadUrl { get; init; } = string.Empty;
    /// <summary>Alias of <see cref="DownloadUrl"/> for existing FA clients.</summary>
    public string PdfUrl { get; init; } = string.Empty;
}

public sealed record TenantInvoiceListResponse
{
    public IReadOnlyList<TenantInvoiceDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalPages { get; init; }
    public int ActiveCount { get; init; }
    public int CancelledCount { get; init; }
}
