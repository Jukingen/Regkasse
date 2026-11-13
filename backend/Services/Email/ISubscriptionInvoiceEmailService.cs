using KasseAPI_Final.Services.Billing;

namespace KasseAPI_Final.Services.Email;

/// <summary>SMTP delivery for SaaS subscription invoices (non-fiscal). Distinct from POS <see cref="IInvoiceEmailService"/>.</summary>
public interface ISubscriptionInvoiceEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendIssuedAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        CancellationToken cancellationToken = default);

    Task<bool> TrySendPaidConfirmationAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        CancellationToken cancellationToken = default);

    Task<bool> TrySendReminderAsync(
        SubscriptionInvoiceDto invoice,
        byte[] pdfContent,
        string? tenantEmail,
        int daysOverdue,
        CancellationToken cancellationToken = default);
}
