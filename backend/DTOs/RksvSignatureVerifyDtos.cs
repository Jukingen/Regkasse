using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class RksvSignatureVerifyRequest
{
    /// <summary>Compact JWS: header.payload.signature (Base64URL, no padding).</summary>
    [Required]
    public string Signature { get; set; } = string.Empty;

    /// <summary>Optional SHA-1 certificate thumbprint. When omitted, the active TSE signing certificate is used.</summary>
    public string? CertificateThumbprint { get; set; }
}

public sealed class RksvSignatureVerifyResponse
{
    public bool Valid { get; set; }

    public string Details { get; set; } = string.Empty;

    public string? CertificateThumbprintUsed { get; set; }
}
