using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Simulated TSE for Development fallback. Uses deterministic pseudo-JWS (no RKSV-valid ECDSA).
/// </summary>
public sealed class SoftTseService : ISoftTseService
{
    private readonly FakeTseProvider _fake;
    private readonly ILogger<SoftTseService> _logger;

    public SoftTseService(FakeTseProvider fake, ILogger<SoftTseService> logger)
    {
        _fake = fake;
        _logger = logger;
    }

    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
        => _fake.IsReadyAsync(cancellationToken);

    public Task<TseSignResult> SignAsync(
        BelegdatenPayload payload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Soft TSE signing (simulated JWS, not legally binding) belegnummer={Belegnummer} correlationId={CorrelationId}",
            payload.Belegnummer,
            correlationId);
        return _fake.SignAsync(payload, correlationId, cancellationToken);
    }
}
