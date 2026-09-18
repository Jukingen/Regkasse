namespace KasseAPI_Final.DTOs;

/// <summary>
/// Shared UStG / EN 16931 document shape. Not an RKSV <see cref="ReceiptDTO"/>.
/// Austria leaves <c>InvoiceDocument.Structured</c> null.
/// </summary>
public sealed class InvoiceDocumentDto
{
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>Steuernummer / seller tax number.</summary>
    public string? SellerTaxNumber { get; init; }

    /// <summary>USt-IdNr / seller VAT ID.</summary>
    public string? SellerVatId { get; init; }

    public string? InvoiceNumber { get; init; }

    public DateTime? InvoiceDate { get; init; }

    /// <summary>Leistungsbeschreibung.</summary>
    public string? PerformanceDescription { get; init; }

    public decimal NetAmount { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal GrossAmount { get; init; }

    public string Currency { get; init; } = "EUR";
}
