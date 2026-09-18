using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>
/// XRechnung XML builder (German CIUS of EN 16931). Syntax generation is not implemented.
/// See <c>docs/FISCAL_GERMANY.md</c>.
/// </summary>
public interface IXrechnungXmlBuilder
{
    Task<string> BuildXmlAsync(InvoiceDocumentDto document, CancellationToken cancellationToken = default);
}
