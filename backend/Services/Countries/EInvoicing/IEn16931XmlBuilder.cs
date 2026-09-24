using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>EN 16931 invoice as UBL 2.1. CII is not produced here.</summary>
public interface IEn16931XmlBuilder
{
    Task<string> BuildXmlAsync(InvoiceDocumentDto document, CancellationToken cancellationToken = default);
}
