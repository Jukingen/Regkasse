using KasseAPI_Final.Services;

namespace KasseAPI_Final.Fiscal;

/// <summary>
/// Single choke point from application layer into <see cref="ITseService.CreateInvoiceSignatureAsync"/> (payload semantics unchanged).
/// </summary>
public static class FiscalTseSigning
{
    public static Task<TseSignatureResult> SignAsync(ITseService tseService, FiscalSigningRequest request)
    {
        ArgumentNullException.ThrowIfNull(tseService);
        ArgumentNullException.ThrowIfNull(request);
        return tseService.CreateInvoiceSignatureAsync(
            request.CashRegisterId,
            request.InvoiceOrBelegNumber,
            request.TotalAmount,
            request.RegisterNumber,
            request.PrevSignatureValue,
            request.Timestamp,
            request.TaxDetailsJson,
            request.DbTransaction);
    }
}
