using System.Security.Cryptography;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Tse;

namespace KasseAPI_Final.Services;

public sealed class RksvSignatureVerifyService : IRksvSignatureVerifyService
{
    private readonly ITseKeyProvider _keyProvider;
    private readonly SignaturePipeline _pipeline;

    public RksvSignatureVerifyService(ITseKeyProvider keyProvider, SignaturePipeline pipeline)
    {
        _keyProvider = keyProvider;
        _pipeline = pipeline;
    }

    public async Task<RksvSignatureVerifyResponse> VerifyAsync(
        string signature,
        string? certificateThumbprint,
        CancellationToken cancellationToken = default)
    {
        var compactJws = signature.Trim();
        if (string.IsNullOrWhiteSpace(compactJws))
        {
            return Fail("Signature is empty.");
        }

        var parts = compactJws.Split('.');
        if (parts.Length != 3)
        {
            return Fail("Compact JWS must have exactly 3 parts (header.payload.signature).");
        }

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                return Fail("Compact JWS contains an empty part.");
            if (part.Contains('='))
                return Fail("Compact JWS must use Base64URL without padding.");
        }

        var (publicKey, thumbprintUsed, resolveError) = await ResolvePublicKeyAsync(certificateThumbprint, cancellationToken)
            .ConfigureAwait(false);
        if (publicKey == null)
            return Fail(resolveError ?? "Unable to resolve signing certificate public key.");

        try
        {
            var valid = _pipeline.Verify(compactJws, publicKey);
            if (valid)
            {
                return new RksvSignatureVerifyResponse
                {
                    Valid = true,
                    CertificateThumbprintUsed = thumbprintUsed,
                    Details = string.IsNullOrWhiteSpace(thumbprintUsed)
                        ? "ES256 verification succeeded."
                        : $"ES256 verification succeeded. Certificate thumbprint: {thumbprintUsed}.",
                };
            }

            return new RksvSignatureVerifyResponse
            {
                Valid = false,
                CertificateThumbprintUsed = thumbprintUsed,
                Details = "ES256 cryptographic verification failed. Signature does not match the certificate public key.",
            };
        }
        catch (TsePipelineException ex)
        {
            return Fail(ex.Message, thumbprintUsed);
        }
    }

    private async Task<(ECDsa? PublicKey, string? ThumbprintUsed, string? Error)> ResolvePublicKeyAsync(
        string? certificateThumbprint,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            var normalizedThumbprint = certificateThumbprint.Trim();
            var cert = await _keyProvider
                .GetCertificateByThumbprintAsync(normalizedThumbprint, cancellationToken)
                .ConfigureAwait(false);
            if (cert == null)
            {
                return (null, null, $"Certificate with thumbprint '{normalizedThumbprint}' was not found.");
            }

            using (cert)
            {
                var parsed = CmcParser.ParseCertificate(cert.RawData);
                return (parsed.PublicKey, normalizedThumbprint.ToUpperInvariant(), null);
            }
        }

        var activeCertBytes = _keyProvider.GetCertificateBytes();
        if (activeCertBytes is { Length: > 0 })
        {
            var parsed = CmcParser.ParseCertificate(activeCertBytes);
            var thumbprint = _keyProvider.GetCurrentCertificateThumbprint()
                ?? TseCertificateThumbprint.ComputeFromDer(activeCertBytes);
            return (parsed.PublicKey, thumbprint, null);
        }

        if (_keyProvider is SoftwareTseKeyProvider softwareProvider)
        {
            return (softwareProvider.GetPublicKey(), _keyProvider.GetCurrentCertificateThumbprint(), null);
        }

        return (null, null, "No signing certificate available on this TSE key provider.");
    }

    private static RksvSignatureVerifyResponse Fail(string details, string? thumbprintUsed = null) =>
        new()
        {
            Valid = false,
            Details = details,
            CertificateThumbprintUsed = thumbprintUsed,
        };
}
