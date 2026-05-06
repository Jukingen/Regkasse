using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Services.Vouchers;

/// <summary>
/// Stateless POS/admin voucher plaintext code generator. Plain codes are returned once to clients only; persisted storage uses hash via <see cref="VoucherCodeHasher"/>.
/// </summary>
internal static class VoucherPlainCodeFactory
{
    public static string GeneratePlainVoucherCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[14];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(20);
        sb.Append("GUT-");
        foreach (var b in bytes)
            sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }

    public static string BuildMaskedCode(string normalized)
    {
        if (normalized.Length <= 4)
            return "****" + normalized;
        return "****" + normalized[^4..];
    }
}
