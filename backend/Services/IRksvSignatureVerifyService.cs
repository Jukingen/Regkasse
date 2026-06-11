using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IRksvSignatureVerifyService
{
    Task<RksvSignatureVerifyResponse> VerifyAsync(
        string signature,
        string? certificateThumbprint,
        CancellationToken cancellationToken = default);
}
