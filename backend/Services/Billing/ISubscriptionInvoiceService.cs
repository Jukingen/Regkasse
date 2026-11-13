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
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
}

public sealed class MonthlyInvoiceGenerationResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

public sealed class MarkPaidRequest
{
    public DateTime? PaidAt { get; set; }
    /// <summary><c>bank_transfer</c>, <c>card</c>, or <c>cash</c>.</summary>
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
}

public sealed class VoidInvoiceRequest
{
    public string? Reason { get; set; }
}

public sealed class SubscriptionInvoiceActionResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public SubscriptionInvoiceDto? Invoice { get; init; }

    public static SubscriptionInvoiceActionResult Ok(SubscriptionInvoiceDto invoice) => new()
    {
        Succeeded = true,
        Invoice = invoice,
    };

    public static SubscriptionInvoiceActionResult Fail(string code, string error) => new()
    {
        Succeeded = false,
        Code = code,
        Error = error,
    };
}

public interface ISubscriptionInvoiceService
{
    Task<IReadOnlyList<SubscriptionInvoiceDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        string? status = null,
        Guid? tenantId = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<SubscriptionInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<byte[]?> GetPdfAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Generate monthly invoices for active paid tenants for the given UTC month (default: previous month).</summary>
    Task<MonthlyInvoiceGenerationResult> GenerateMonthlyInvoicesAsync(
        DateTime? periodMonthUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a monthly invoice should be created for this tenant/period
    /// (trial + prepaid coverage gates). Duplicate-period check is separate.
    /// </summary>
    Task<bool> ShouldGenerateInvoiceAsync(
        Guid tenantId,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    Task<SubscriptionInvoiceActionResult> MarkPaidAsync(
        Guid id,
        MarkPaidRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionInvoiceActionResult> VoidAsync(
        Guid id,
        VoidInvoiceRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
