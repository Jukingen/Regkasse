using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// Input for <see cref="IInvoiceStrategy.AllocateReceiptNumberAsync"/>. Carries the register because
/// fiscal counters are per cash register and per UTC day, not per company.
/// </summary>
public sealed record ReceiptNumberAllocationContext
{
    public required Guid CashRegisterId { get; init; }

    /// <summary>Retries when the counter allocation races. Default matches the sequence service default.</summary>
    public int MaxAttempts { get; init; } = 3;
}

/// <summary>
/// Country-neutral wrapper around the document the existing services already produce. Austria returns
/// the RKSV <see cref="ReceiptDTO"/> unchanged — no field is re-derived here.
/// </summary>
public sealed record InvoiceDocument(string CountryCode, ReceiptDTO Receipt);

/// <summary>
/// One legally mandated disclosure, as a **requirement** rather than a rendered value: the printed
/// values come from <see cref="IInvoiceStrategy.BuildInvoiceDocumentAsync"/>. Keys are stable
/// identifiers, never user-facing text, so no i18n string is invented here.
/// </summary>
/// <param name="Key">Stable identifier, e.g. <c>seller.vatId</c>.</param>
/// <param name="LegalBasis">Statute the requirement comes from, e.g. <c>UStG §11</c>.</param>
/// <param name="SourceField">Entity field or DTO member that supplies the value.</param>
public sealed record DisclosureRequirement(string Key, string LegalBasis, string SourceField);
