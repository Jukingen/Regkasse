using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IInvoiceService
{
    /// <summary>
    /// Invoice DTO from payment: persisted invoice when present, otherwise ephemeral build (payment company snapshot).
    /// </summary>
    Task<InvoiceDto> GenerateInvoiceAsync(PaymentDetails payment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves invoice entity for PDF/API: DB row, or synthesized from payment with RKSV seller snapshot.
    /// </summary>
    Task<Invoice> ResolveInvoiceFromPaymentAsync(PaymentDetails payment, CancellationToken cancellationToken = default);
}
