using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Development-only signer: deterministic pseudo-JWS whose payload is the RKSV machine code.
    /// The signature segment is not cryptographically valid. No physical TSE required.
    /// </summary>
    public sealed class FakeTseProvider : ITseProvider
    {
        public const string FakeCertificateSerial = "SIM-TEST";

        private readonly ILogger<FakeTseProvider> _logger;

        public FakeTseProvider(ILogger<FakeTseProvider> logger)
        {
            _logger = logger;
        }

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<TseSignResult> SignAsync(BelegdatenPayload payload, string correlationId, CancellationToken cancellationToken = default)
        {
            var compact = BuildDeterministicPseudoJws(payload, correlationId);
            _logger.LogDebug("FakeTseProvider signed belegnummer={Belegnummer}, correlationId={CorrelationId}, length={Length}",
                payload.Belegnummer, correlationId, compact.Length);
            return Task.FromResult(new TseSignResult(compact, FakeCertificateSerial));
        }

    /// <summary>
    /// Builds header.payload.signature. The payload segment is the RKSV §9 machine code
    /// (<c>_R1-AT1_…</c>) so development receipts can render a QR. The signature segment
    /// stays a deterministic pseudo-value and is lengthened to stress-test DB text columns.
    /// This is not a legally binding TSE signature.
    /// </summary>
    public static string BuildDeterministicPseudoJws(BelegdatenPayload payload, string correlationId)
    {
        var machineCode = SignaturePipeline.GetMachineCode(payload);
        var header = ToBase64Url("""{"alg":"SIM","typ":"JWT"}""");
        var body = ToBase64Url(machineCode);

        var seed = $"{correlationId}|{payload.SigVorigerBeleg}|{payload.Belegnummer}|{payload.BetragSatzNormal}|{payload.KassenId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var sigCore = ToBase64Url(hash);
        // Repeat to simulate long RSA/ECDSA JWS third segment (DB varchar stress)
        var longSig = string.Concat(Enumerable.Repeat(sigCore, 32));

        return $"{header}.{body}.{longSig}";
    }

        private static string ToBase64Url(string utf8)
        {
            return ToBase64Url(Encoding.UTF8.GetBytes(utf8));
        }

        private static string ToBase64Url(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
