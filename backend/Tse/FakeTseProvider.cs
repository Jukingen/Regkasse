using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// Development-only signer: deterministic pseudo-JWS (no cryptographic validity). No physical TSE required.
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
        /// Builds header.payload.signature with Base64url-like segments. Signature segment is lengthened to stress-test DB text columns.
        /// </summary>
        public static string BuildDeterministicPseudoJws(BelegdatenPayload payload, string correlationId)
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var header = ToBase64Url("""{"alg":"SIM","typ":"JWT"}""");
            var body = ToBase64Url(payloadJson);

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
