using Microsoft.EntityFrameworkCore.Storage;

namespace KasseAPI_Final.Fiscal;

/// <summary>
/// Fiscal-only input for TSE invoice signing. Built from persisted register / allocation state, not API DTOs.
/// </summary>
public sealed record FiscalSigningRequest(
    Guid CashRegisterId,
    string InvoiceOrBelegNumber,
    decimal TotalAmount,
    string RegisterNumber,
    string? PrevSignatureValue = null,
    DateTime? Timestamp = null,
    string? TaxDetailsJson = null,
    IDbContextTransaction? DbTransaction = null);
