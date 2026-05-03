using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>
/// Normalizes and hashes POS voucher codes. Plaintext codes must never be persisted.
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

    public static string Sha256Hex(string normalizedCode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string HashNormalized(string normalizedCode) => Sha256Hex(normalizedCode);
}
