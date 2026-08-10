using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;

namespace KasseAPI_Final.Services.Billing;

public sealed class SubscriptionInvoiceDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public LicenseType LicenseType { get; set; }
    public decimal AmountNet { get; set; }
    public decimal VatRate { get; set; }
    public decimal AmountVat { get; set; }
    public decimal AmountGross { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
}

public sealed class MonthlyInvoiceGenerationResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

public interface ISubscriptionInvoiceService
{
    Task<IReadOnlyList<SubscriptionInvoiceDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<SubscriptionInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Generate monthly invoices for active paid tenants for the given UTC month (default: previous month).</summary>
    Task<MonthlyInvoiceGenerationResult> GenerateMonthlyInvoicesAsync(
        DateTime? periodMonthUtc = null,
        CancellationToken cancellationToken = default);
}
