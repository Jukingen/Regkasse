using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV Checklist 1–5 uyumlu SignaturePipeline.
    /// signatureValue = TAM COMPACT JWS (header.payload.signature)
    /// </summary>
    public class SignaturePipeline
    {
        private readonly ITseKeyProvider _keyProvider;
        private readonly ILogger<SignaturePipeline> _logger;

        private static readonly JsonSerializerOptions DeterministicOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public SignaturePipeline(ITseKeyProvider keyProvider, ILogger<SignaturePipeline> logger)
        {
            _keyProvider = keyProvider;
            _logger = logger;
        }

        /// <summary>
        /// Belegdaten üzerinde COMPACT JWS imzası oluşturur.
        /// </summary>
        /// <param name="payload">Belegdaten payload (Checklist 2: deterministik sıralama)</param>
        /// <param name="correlationId">Structured logging için</param>
        public string Sign(BelegdatenPayload payload, string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString("N")[..12];
            _logger.LogInformation("SignaturePipeline.Sign started, correlationId={CorrelationId}, step=init", correlationId);

            // Checklist 2: JWS Header
            var header = new { alg = "ES256", typ = "JWT" };
            var headerJson = JsonSerializer.SerializeToUtf8Bytes(header);
            var headerB64 = TseCryptoHelper.ToBase64UrlNoPadding(headerJson);
            _logger.LogDebug("SignaturePipeline.Sign correlationId={CorrelationId}, step=header_encoded", correlationId);

            // Checklist 2: Payload deterministik sıralama
            var payloadJson = JsonSerializer.SerializeToUtf8Bytes(payload, DeterministicOptions);
            var payloadB64 = TseCryptoHelper.ToBase64UrlNoPadding(payloadJson);
            _logger.LogDebug("SignaturePipeline.Sign correlationId={CorrelationId}, step=payload_encoded", correlationId);

            // signingInput = header + "." + payload
            var signingInput = $"{headerB64}.{payloadB64}";
            _logger.LogInformation("SignaturePipeline.Sign correlationId={CorrelationId}, step=signing_input_ready", correlationId);

            // Checklist 3: SHA-256 hash on signingInput
            var hashBytes = TseCryptoHelper.Sha256Hash(signingInput);
            _logger.LogDebug("SignaturePipeline.Sign correlationId={CorrelationId}, step=sha256_done", correlationId);

            // Checklist 4: ES256 sign (ECDSA P-256) - raw R||S 64 byte
            var key = _keyProvider.GetSigningKey();
            byte[] rawSignature;
            try
            {
                rawSignature = key.SignHash(hashBytes, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }
            catch
            {
                var derBytes = key.SignHash(hashBytes);
                rawSignature = derBytes.Length == 64 ? derBytes : ConvertDerToRawRs(derBytes);
            }
            _logger.LogDebug("SignaturePipeline.Sign correlationId={CorrelationId}, step=es256_signed", correlationId);

            // Checklist 5: Base64URL no-padding
            var signatureB64 = TseCryptoHelper.ToBase64UrlNoPadding(rawSignature);
            _logger.LogInformation("SignaturePipeline.Sign completed, correlationId={CorrelationId}, step=done", correlationId);

            return $"{signingInput}.{signatureB64}";
        }

        /// <summary>
        /// RKSV Checklist 1–5 diagnostik doğrulama. Her adım için PASS/FAIL ve evidence döner.
        /// </summary>
        public IReadOnlyList<SignatureDiagnosticStep> VerifyDiagnostic(string compactJws, string? correlationId = null)
        {
            var steps = new List<SignatureDiagnosticStep>();
            correlationId ??= Guid.NewGuid().ToString("N")[..12];

            // Step 1: CMC match
            var (step1Status, step1Evidence) = CheckCmcMatch();
            steps.Add(new SignatureDiagnosticStep(1, "CMC match", step1Status, step1Evidence));

            if (string.IsNullOrWhiteSpace(compactJws))
            {
                steps.Add(new SignatureDiagnosticStep(2, "JWS format", "FAIL", "Empty input"));
                steps.Add(new SignatureDiagnosticStep(3, "Hash", "FAIL", "N/A"));
                steps.Add(new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "N/A"));
                steps.Add(new SignatureDiagnosticStep(5, "Base64URL padding", "FAIL", "N/A"));
                return steps;
            }

            var parts = compactJws.Split('.');

            // Step 2: JWS format (3 parts)
            var (step2Status, step2Evidence) = CheckJwsFormat(parts);
            steps.Add(new SignatureDiagnosticStep(2, "JWS format", step2Status, step2Evidence));

            // Step 5: Base64URL padding (check before decode)
            var (step5Status, step5Evidence) = CheckBase64UrlPadding(parts);
            steps.Add(new SignatureDiagnosticStep(5, "Base64URL padding", step5Status, step5Evidence));

            byte[]? signatureBytes = null;
            byte[]? hashBytes = null;
            var signingInput = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : string.Empty;

            // Step 3: Hash
            if (parts.Length == 3 && step5Status == "PASS")
            {
                try
                {
                    signatureBytes = TseCryptoHelper.FromBase64UrlNoPadding(parts[2]);
                    hashBytes = TseCryptoHelper.Sha256Hash(signingInput);
                    steps.Add(new SignatureDiagnosticStep(3, "Hash", "PASS", $"SHA-256({signingInput.Length} chars)"));
                }
                catch (Exception ex)
                {
                    steps.Add(new SignatureDiagnosticStep(3, "Hash", "FAIL", ex.Message));
                }
            }
            else
            {
                steps.Add(new SignatureDiagnosticStep(3, "Hash", "FAIL", "Invalid JWS or padding"));
            }

            // Step 4: Signature verify
            var publicKey = GetPublicKeyForVerify();
            if (publicKey != null && signatureBytes != null && hashBytes != null)
            {
                try
                {
                    var valid = signatureBytes.Length == 64
                        ? publicKey.VerifyHash(hashBytes, signatureBytes, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                        : publicKey.VerifyHash(hashBytes, signatureBytes);
                    steps.Add(new SignatureDiagnosticStep(4, "Signature verify", valid ? "PASS" : "FAIL",
                        valid ? "ES256 verification succeeded" : "ES256 verification failed"));
                }
                catch (Exception ex)
                {
                    steps.Add(new SignatureDiagnosticStep(4, "Signature verify", "FAIL", ex.Message));
                }
            }
            else
            {
                var idx = steps.FindIndex(s => s.StepId == 4);
                if (idx < 0)
                    steps.Add(new SignatureDiagnosticStep(4, "Signature verify", "FAIL", "Missing key or signature data"));
            }

            // Reorder: 1,2,3,4,5
            return steps.OrderBy(s => s.StepId).ToList();
        }

        private (string status, string? evidence) CheckCmcMatch()
        {
            var certBytes = _keyProvider.GetCertificateBytes();
            if (certBytes != null && certBytes.Length > 0)
            {
                try
                {
                    var result = CmcParser.ParseCertificate(certBytes);
                    CmcParser.ValidateKeyMatch(result.PublicKey, _keyProvider.GetSigningKey());
                    return ("PASS", $"Certificate serial {result.SerialNumber} matches signing key");
                }
                catch (TsePipelineException ex)
                {
                    return ("FAIL", ex.Message);
                }
            }
            return ("PASS", "Software mode: key provider used (no CMC)");
        }

        private static (string status, string? evidence) CheckJwsFormat(string[] parts)
        {
            if (parts.Length != 3)
                return ("FAIL", $"Expected 3 parts, got {parts.Length}");
            try
            {
                foreach (var p in parts)
                {
                    if (string.IsNullOrEmpty(p)) return ("FAIL", "Empty part");
                    if (p.Contains('=')) return ("FAIL", "Part contains padding (see Base64URL step)");
                    _ = TseCryptoHelper.FromBase64UrlNoPadding(p);
                }
                return ("PASS", "header.payload.signature valid");
            }
            catch
            {
                return ("FAIL", "Invalid Base64URL in one or more parts");
            }
        }

        private static (string status, string? evidence) CheckBase64UrlPadding(string[] parts)
        {
            foreach (var p in parts)
            {
                if (p.Contains('='))
                    return ("FAIL", "Padding '=' found in part");
            }
            return ("PASS", "No padding in any part");
        }

        private ECDsa? GetPublicKeyForVerify()
        {
            var certBytes = _keyProvider.GetCertificateBytes();
            if (certBytes != null && certBytes.Length > 0)
            {
                try
                {
                    var result = CmcParser.ParseCertificate(certBytes);
                    return result.PublicKey;
                }
                catch { return null; }
            }
            if (_keyProvider is SoftwareTseKeyProvider sw)
                return sw.GetPublicKey();
            return _keyProvider.GetSigningKey();
        }

        /// <summary>
        /// COMPACT JWS doğrular. Verify PASS → true.
        /// </summary>
        public bool Verify(string compactJws, ECDsa publicKey, string? correlationId = null)
        {
            correlationId ??= Guid.NewGuid().ToString("N")[..12];
            _logger.LogInformation("SignaturePipeline.Verify started, correlationId={CorrelationId}, step=init", correlationId);

            var parts = compactJws.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("SignaturePipeline.Verify correlationId={CorrelationId}, step=fail, reason=invalid_parts_count", correlationId);
                throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "Compact JWS must have 3 parts");
            }

            var signingInput = $"{parts[0]}.{parts[1]}";
            var signatureB64 = parts[2];

            // Checklist 5: Base64URL decode (padding hatası kontrolü)
            byte[] signatureBytes;
            try
            {
                signatureBytes = TseCryptoHelper.FromBase64UrlNoPadding(signatureB64);
            }
            catch (TsePipelineException ex) when (ex.ErrorCode == "BASE64URL_PADDING_ERROR")
            {
                _logger.LogWarning("SignaturePipeline.Verify correlationId={CorrelationId}, step=fail, error=BASE64URL_PADDING_ERROR", correlationId);
                throw;
            }

            // raw R||S 64 byte veya DER
            byte[] hashBytes;
            try
            {
                hashBytes = TseCryptoHelper.Sha256Hash(signingInput);
            }
            catch
            {
                _logger.LogWarning("SignaturePipeline.Verify correlationId={CorrelationId}, step=fail, reason=corrupted_payload", correlationId);
                throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "Corrupted payload");
            }

            bool valid = signatureBytes.Length == 64
                ? publicKey.VerifyHash(hashBytes, signatureBytes, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
                : publicKey.VerifyHash(hashBytes, signatureBytes);

            _logger.LogInformation("SignaturePipeline.Verify completed, correlationId={CorrelationId}, step=done, valid={Valid}", correlationId, valid);
            return valid;
        }

        private static byte[] ConvertDerToRawRs(byte[] derSignature)
        {
            try
            {
                return ECDsaSignatureHelper.ConvertDerToRawRs(derSignature);
            }
            catch
            {
                throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "Failed to convert DER signature to raw R||S");
            }
        }
    }

    /// <summary>
    /// ECDSA DER format → raw R||S 64 byte (Checklist 4).
    /// </summary>
    internal static class ECDsaSignatureHelper
    {
        public static byte[] ConvertDerToRawRs(byte[] der)
        {
            if (der == null || der.Length < 8)
                throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "DER signature too short");

            int offset = 0;
            if (der[offset++] != 0x30) throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "Invalid DER: expected SEQUENCE");
            int seqLen = ReadDerLength(der, ref offset);
            if (offset + seqLen > der.Length) throw new TsePipelineException("INVALID_SIGNATURE_FORMAT", "Invalid DER: truncated");

            int rLen = ReadDerInteger(der, ref offset, out byte[] r);
            int sLen = ReadDerInteger(der, ref offset, out byte[] s);

            const int coordinateSize = 32;
            var result = new byte[64];
            CopyPadded(r, result, 0, coordinateSize);
            CopyPadded(s, result, coordinateSize, coordinateSize);
            return result;
        }

        private static int ReadDerLength(byte[] data, ref int offset)
        {
            if (offset >= data.Length) return 0;
            byte b = data[offset++];
            if (b < 0x80) return b;
            int lenLen = b & 0x7F;
            int len = 0;
            for (int i = 0; i < lenLen && offset < data.Length; i++)
                len = (len << 8) | data[offset++];
            return len;
        }

        private static int ReadDerInteger(byte[] data, ref int offset, out byte[] value)
        {
            if (offset >= data.Length || data[offset++] != 0x02)
            {
                value = Array.Empty<byte>();
                return 0;
            }
            int len = ReadDerLength(data, ref offset);
            value = new byte[len];
            Buffer.BlockCopy(data, offset, value, 0, len);
            offset += len;
            if (value.Length > 0 && value[0] == 0)
            {
                var trimmed = new byte[value.Length - 1];
                Buffer.BlockCopy(value, 1, trimmed, 0, trimmed.Length);
                value = trimmed;
            }
            return len;
        }

        private static void CopyPadded(byte[] src, byte[] dst, int dstOffset, int size)
        {
            if (src.Length >= size)
            {
                Buffer.BlockCopy(src, src.Length - size, dst, dstOffset, size);
            }
            else
            {
                int pad = size - src.Length;
                Buffer.BlockCopy(src, 0, dst, dstOffset + pad, src.Length);
            }
        }
    }
}
