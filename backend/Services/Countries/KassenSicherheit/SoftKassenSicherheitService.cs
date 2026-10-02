using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Countries.KassenSicherheit;

/// <summary>
/// Development signer for DE KassenSicherheit. Deterministic pseudo-JWS, not legally binding.
/// Mirrors SoftTseService: no outbound HTTP and no Austrian RKSV payload type.
/// </summary>
public sealed class SoftKassenSicherheitService : IKassenSicherheitService
{
    public const string ProviderId = "soft";
    public const string CertificateSerial = "SIM-TEST";

    private readonly ILogger<SoftKassenSicherheitService> _logger;

    public SoftKassenSicherheitService(ILogger<SoftKassenSicherheitService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<KassenSicherheitSignResult> SignAsync(
        KassenSicherheitSignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogWarning(
            "Soft KassenSicherheit signing (simulated, not legally binding) tenant={TenantId} cashRegister={CashRegisterId}",
            request.TenantId,
            request.CashRegisterId);

        var signature = BuildPseudoJws(request.Payload ?? string.Empty);
        return Task.FromResult(new KassenSicherheitSignResult(true, signature, ProviderId));
    }

    public Task<KassenSicherheitStatusResult> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new KassenSicherheitStatusResult(true, "ready", ProviderId));
    }

    public Task<KassenSicherheitCertificateChainResult> GetCertificateChainAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new KassenSicherheitCertificateChainResult(
            ProviderId,
            [CertificateSerial]));
    }

    internal static string BuildPseudoJws(string payload)
    {
        var header = ToBase64Url("""{"alg":"SIM","typ":"JWT"}""");
        var body = ToBase64Url(payload);
        var signature = ToBase64Url("soft-kassensicherheit:" + payload);
        return header + "." + body + "." + signature;
    }

    private static string ToBase64Url(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
