using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Offline;

namespace KasseAPI_Final.Services.Countries.Strategies.Austria;

/// <summary>
/// Austrian receipt adapter. Numbering delegates to <see cref="ISequenceReservationService"/> (gap-free
/// per register and UTC day, with retry) and the document to <see cref="IReceiptService"/>, which is the
/// service pinned by the AT receipt-disclosure baseline. No RKSV field is rebuilt here.
///
/// Depends on scoped services — registered as scoped.
/// </summary>
public sealed class AustriaInvoiceStrategy : IInvoiceStrategy
{
    /// <summary>
    /// Disclosures an Austrian receipt must show (§11 UStG identity and VAT data, RKSV §8 signature
    /// block). Values are read off the generated <c>ReceiptDTO</c>; this list only names them.
    /// </summary>
    private static readonly IReadOnlyList<DisclosureRequirement> AustrianDisclosures =
    [
        new("seller.name", "UStG §11", nameof(CompanySettings.CompanyName)),
        new("seller.address", "UStG §11", nameof(CompanySettings.CompanyAddress)),
        new("seller.vatId", "UStG §11", nameof(CompanySettings.CompanyTaxNumber)),
        new("receipt.number", "RKSV §8", nameof(PaymentDetails.ReceiptNumber)),
        new("receipt.issuedAt", "UStG §11", nameof(PaymentDetails.CreatedAt)),
        new("receipt.totalGross", "UStG §11", nameof(PaymentDetails.TotalAmount)),
        new("receipt.vatBreakdown", "UStG §11", nameof(PaymentDetails.TaxDetails)),
        new("rksv.signature", "RKSV §8", nameof(PaymentDetails.TseSignature)),
    ];

    private readonly ISequenceReservationService _sequenceReservation;
    private readonly IReceiptService _receiptService;

    public AustriaInvoiceStrategy(
        ISequenceReservationService sequenceReservation,
        IReceiptService receiptService)
    {
        _sequenceReservation = sequenceReservation;
        _receiptService = receiptService;
    }

    public string CountryCode => CountryProfileCodes.Austria;

    public Task<string> AllocateReceiptNumberAsync(
        ReceiptNumberAllocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _sequenceReservation.ReserveNextReceiptNumberAsync(
            context.CashRegisterId,
            cancellationToken,
            context.MaxAttempts);
    }

    public async Task<InvoiceDocument> BuildInvoiceDocumentAsync(
        PaymentDetails payment,
        CompanySettings company,
        Customer? customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(company);
        cancellationToken.ThrowIfCancellationRequested();

        // ReceiptService resolves the company header itself (payment snapshot → live CompanySettings),
        // so passing company/customer here must not introduce a second header source.
        var receipt = await _receiptService.GenerateReceiptAsync(payment).ConfigureAwait(false);

        return new InvoiceDocument(CountryCode, receipt);
    }

    public IReadOnlyList<DisclosureRequirement> GetMandatoryDisclosures(
        CompanySettings company,
        Customer? customer)
    {
        ArgumentNullException.ThrowIfNull(company);

        // Austrian POS receipts carry no customer-specific disclosure, so the list is constant.
        return AustrianDisclosures;
    }
}
