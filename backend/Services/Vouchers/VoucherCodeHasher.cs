using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>
/// RKSV/GDPR: persisted voucher identifiers use SHA-256 of the canonical (normalized UTF-8) code.
/// Plaintext codes must never be written to persistent storage or application logs — use <see cref="MaskPlainCodeForLog"/> for diagnostics only.
/// </summary>
public static class VoucherCodeHasher
{
    /// <summary>Trim and uppercase ASCII for stable lookup (operator-entered codes).</summary>
    public static string NormalizeCode(string voucherCode)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
            return string.Empty;
        return voucherCode.Trim().ToUpperInvariant();
    }

    /// <summary>SHA-256 over UTF-8 bytes of <paramref name="normalizedCode"/>; lowercase hex encoding (persisted as <see cref="Models.Voucher.CodeHash"/>).</summary>
    public static string Sha256Hex(string normalizedCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>Same as <see cref="Sha256Hex"/>; stored hash column value.</summary>
    public static string HashNormalized(string normalizedCode) => Sha256Hex(normalizedCode);

    /// <summary>Optional correlation label for logs — first 16 hex chars of hash (nothing reversible).</summary>
    public static string HashCorrelationPrefix(string normalizedCode, int hexChars = 16)
    {
        var hex = Sha256Hex(normalizedCode);
        hexChars = Math.Clamp(hexChars, 8, hex.Length);
        return hex[..hexChars];
    }

    /// <summary>
    /// Masks plaintext for structured logs (RKSV-sensitive). Empty input returns "(empty)". Invalid for storage — persisted hint is <see cref="Models.Voucher.MaskedCode"/>.
    /// </summary>
    public static string MaskPlainCodeForLog(string? rawVoucherCode)
    {
        if (string.IsNullOrWhiteSpace(rawVoucherCode))
            return "(empty)";
        var norm = NormalizeCode(rawVoucherCode);
        if (norm.Length == 0)
            return "(empty)";
        return VoucherPlainCodeFactory.BuildMaskedCode(norm);
    }

    /// <summary>Prefer <see cref="MaskPlainCodeForLog"/> instead of emitting raw voucher input in diagnostic messages.</summary>
    public static string SanitizeDiagnosticFragment(string messageFragment, string? rawVoucherCodeInMessage)
    {
        if (string.IsNullOrEmpty(rawVoucherCodeInMessage))
            return messageFragment;
        var mask = MaskPlainCodeForLog(rawVoucherCodeInMessage);
        return messageFragment.Replace(rawVoucherCodeInMessage, mask, StringComparison.Ordinal);
    }
}
