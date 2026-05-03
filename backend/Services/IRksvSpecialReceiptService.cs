using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IRksvSpecialReceiptService
{
    /// <summary>
    /// RKSV Monats-Nullbeleg: zero TSE-signed receipt in normal Beleg sequence (not <c>DAILY_</c> closing prefix).
    /// </summary>
    /// <exception cref="InvalidOperationException">Duplicate month, TSE not ready, register missing, etc.</exception>
    Task<CreateNullbelegResponse> CreateNullbelegAsync(
        CreateNullbelegRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
