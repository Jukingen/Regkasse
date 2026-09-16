using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>
/// FA list of TSE-signed Monatsbeleg / December Jahresbeleg receipts (DEP source: payment_details).
/// Creation stays on <see cref="IRksvSpecialReceiptService.CreateMonatsbelegAsync"/>.
/// </summary>
public interface IRksvMonatsbelegService
{
    Task<MonatsbelegListResponse> ListAsync(
        MonatsbelegListQuery query,
        CancellationToken cancellationToken = default);
}
