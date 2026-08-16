using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development Soft TSE: local simulated compact JWS (not legally binding).
/// Delegates to <see cref="FakeTseProvider"/> — does not invent a second signing stack
/// and does not replace RKSV <c>SignaturePipeline</c> for fiscal receipts.
/// </summary>
public interface ISoftTseService
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    Task<TseSignResult> SignAsync(
        BelegdatenPayload payload,
        string correlationId,
        CancellationToken cancellationToken = default);
}
