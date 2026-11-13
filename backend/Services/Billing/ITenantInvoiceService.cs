namespace KasseAPI_Final.Services.Billing;

/// <summary>Tenant-scoped license invoices for Mandanten-Admin self-service.</summary>
public interface ITenantInvoiceService
{
    Task<TenantInvoiceListResponse> GetInvoicesForTenantAsync(
        Guid tenantId,
        int page = 1,
        int pageSize = 20,
        string? status = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns PDF bytes for a sale owned by <paramref name="tenantId"/>; otherwise throws <see cref="KeyNotFoundException"/>.</summary>
    Task<(byte[] Pdf, string FileName)> GetInvoicePdfForTenantAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
