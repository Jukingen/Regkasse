using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Email;

public interface IInvoiceEmailService
{
    bool IsConfigured { get; }

    Task<bool> TrySendInvoiceAsync(
        Invoice invoice,
        byte[] pdfContent,
        string recipientEmail,
        CancellationToken cancellationToken = default);
}
