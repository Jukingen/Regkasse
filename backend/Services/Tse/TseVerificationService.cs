using System.Linq;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development-aware TSE signature verification. Real crypto when TSE is live;
/// simulated FakeTseProvider JWS is accepted only under TSE simulation / demo.
/// </summary>
public sealed class TseVerificationService : ITseVerificationService
{
    public const string SimulatedDevelopmentMessage = "Simulierte Signatur (Entwicklungsumgebung)";

    private readonly IRksvSignatureVerifyService _realVerify;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<TseVerificationService> _logger;

    public TseVerificationService(
        IRksvSignatureVerifyService realVerify,
        IRksvEnvironmentService rksvEnvironment,
        IHostEnvironment hostEnvironment,
        ILogger<TseVerificationService> logger)
    {
        _realVerify = realVerify;
        _rksvEnvironment = rksvEnvironment;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<TseVerificationResult> VerifySignatureAsync(
        string signature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return new TseVerificationResult
            {
                IsValid = false,
                IsSimulated = false,
                Message = "Signature is empty.",
            };
        }

        var trimmed = signature.Trim();

        // TSE simulation / demo (includes Development via RksvEnvironmentService.IsDemoMode).
        // FakeTseProvider emits non-cryptographic pseudo-JWS — do not treat crypto FAIL as fiscal invalidity.
        if (_rksvEnvironment.IsTseSimulated() && LooksLikePresentableSignature(trimmed))
        {
            _logger.LogDebug(
                "Accepting simulated TSE signature in {Environment}",
                _hostEnvironment.EnvironmentName);

            return new TseVerificationResult
            {
                IsValid = true,
                IsSimulated = true,
                Message = SimulatedDevelopmentMessage,
            };
        }

        var real = await _realVerify
            .VerifyAsync(trimmed, certificateThumbprint: null, cancellationToken)
            .ConfigureAwait(false);

        return new TseVerificationResult
        {
            IsValid = real.Valid,
            IsSimulated = false,
            Message = real.Details,
        };
    }

    /// <summary>
    /// Accept non-empty compact JWS (or legacy sim_ prefix). Reject empty / whitespace-only.
    /// </summary>
    internal static bool LooksLikePresentableSignature(string signature)
    {
        if (signature.StartsWith("sim_", StringComparison.OrdinalIgnoreCase))
            return true;

        var parts = signature.Split('.');
        return parts.Length == 3 && parts.All(static p => !string.IsNullOrEmpty(p));
    }
}
