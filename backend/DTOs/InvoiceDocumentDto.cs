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

    public string? SellerName { get; init; }

    public string? SellerStreet { get; init; }

    public string? SellerCity { get; init; }

    public string? SellerPostalCode { get; init; }

    public string? SellerCountry { get; init; }

    public string? BuyerName { get; init; }

    public string? BuyerVatId { get; init; }

    public string? BuyerStreet { get; init; }

    public string? BuyerCity { get; init; }

    public string? BuyerPostalCode { get; init; }

    public string? BuyerCountry { get; init; }

    /// <summary>UNTDID 5305: S, Z, AE, E, K, G, O.</summary>
    public string VatCategory { get; init; } = "S";

    public decimal VatPercent { get; init; }

    public string? TaxExemptionReason { get; init; }

    public string? TaxExemptionReasonCode { get; init; }
}
