using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Country-specific receipt / invoice behavior. Implementations are **adapters** over the services
/// that already own numbering and document building.
///
/// TSE signing input, machine code, and QR payload are deliberately **not** part of this contract:
/// they belong to the signature pipeline (<c>BelegdatenPayloadBuilder</c>), not to document layout.
/// Resolved through <see cref="IInvoiceStrategyResolver"/>; no domain flow calls this yet.
/// </summary>
public interface IInvoiceStrategy
{
    /// <summary>Profile code this strategy serves (<see cref="CountryProfileCodes"/>).</summary>
    string CountryCode { get; }

    /// <summary>
    /// Allocates the next fiscal document number for the register. Austria returns a Belegnummer
    /// (<c>payment_details.receipt_number</c>); the billing invoice sequence is a different counter and
    /// is out of scope here.
    /// </summary>
    Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Builds the customer-facing fiscal document for a persisted payment.</summary>
    Task<InvoiceDocument> BuildInvoiceDocumentAsync(
        PaymentDetails payment,
        CompanySettings company,
        Customer? customer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disclosures the document must carry in this country. Metadata only — the values live on the
    /// document returned by <see cref="BuildInvoiceDocumentAsync"/>.
    /// </summary>
    IReadOnlyList<DisclosureRequirement> GetMandatoryDisclosures(CompanySettings company, Customer? customer);
}
