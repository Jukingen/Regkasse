namespace KasseAPI_Final.Services.Billing;

public sealed record CreateLicenseSaleRequest(
    Guid TenantId,
    string LicensePlan,
    DateTime? CustomValidUntilUtc,
    decimal PriceNet,
    decimal VatRate = 20.00m,
    string? Notes = null);

public sealed record LicenseSaleResponse(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    string LicenseKey,
    string LicensePlan,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    decimal PriceNet,
    decimal VatRate,
    decimal VatAmount,
    decimal PriceGross,
    string Currency,
    string InvoiceNumber,
    string? InvoicePdfPath,
    string Status,
    DateTime SoldAtUtc,
    string SoldByUserId,
    string? Notes);

public sealed record LicenseSalePreviewRequest(
    Guid TenantId,
    string LicensePlan,
    DateTime? CustomValidUntilUtc,
    decimal PriceNet,
    decimal VatRate = 20.00m);

public sealed record LicenseSalePreviewResponse(
    string LicenseKey,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    decimal PriceNet,
    decimal VatRate,
    decimal VatAmount,
    decimal PriceGross,
    string InvoiceNumber,
    string TenantName,
    string TenantSlug,
    string TenantAddress,
    string TenantVatId,
    string TenantEmail);

public sealed record CancelLicenseSaleRequest(
    string CancellationReason);

public sealed record LicenseSaleListQuery(
    int Page = 1,
    int PageSize = 20,
    string? TenantId = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null);

public sealed record LicenseSaleListResponse(
    List<LicenseSaleResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record LicenseSaleStatsResponse(
    decimal TotalRevenueNet,
    decimal TotalRevenueGross,
    decimal TotalVat,
    int TotalSales,
    int ActiveLicenses,
    int ExpiringSoonLicenses);
