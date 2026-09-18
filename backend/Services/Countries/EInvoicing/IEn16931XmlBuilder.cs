using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Countries.EInvoicing;

/// <summary>EN 16931 semantic invoice XML. Syntax (UBL vs CII) is not implemented.</summary>
public interface IEn16931XmlBuilder
{
    Task<string> BuildXmlAsync(InvoiceDocumentDto document, CancellationToken cancellationToken = default);
}
