using System.Text.Json;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// RKSV-compliant invoice view for POS and PDF. Seller block maps to persisted <see cref="Models.Invoice"/> company fields.
/// </summary>
public sealed class InvoiceDto
{
    public Guid? InvoiceId { get; init; }
    public Guid PaymentId { get; init; }

    /// <summary>RKSV §8 — Unternehmensbezeichnung (seller).</summary>
    public string SellerName { get; init; } = string.Empty;

    /// <summary>RKSV §8 — Sitz der gewerblichen Betriebsstätte.</summary>
    public string SellerAddress { get; init; } = string.Empty;

    /// <summary>RKSV §8 — UID / Steuernummer.</summary>
    public string SellerTaxNumber { get; init; } = string.Empty;

    public string? SellerPhone { get; init; }
    public string? SellerEmail { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;
    public DateTime InvoiceDate { get; init; }
    public DateTime DueDate { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerTaxNumber { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public string TseSignature { get; init; } = string.Empty;
    public string KassenId { get; init; } = string.Empty;
    public Guid CashRegisterId { get; init; }
    public DateTime? TseTimestamp { get; init; }
    public string? PaymentMethod { get; init; }
    public JsonDocument? InvoiceItems { get; init; }
    public JsonDocument? TaxDetails { get; init; }
    public string DataProvenance { get; init; } = "Persisted";
}
