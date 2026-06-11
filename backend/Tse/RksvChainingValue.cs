using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Tse;

/// <summary>
/// RKSV Sig-Voriger-Beleg (Detailspezifikation Abs. 4 / Prozess 2.4).
/// </summary>
public static class RksvChainingValue
{
    /// <summary>
    /// Computes Sig-Voriger-Beleg: SHA-256(input UTF-8), first N bytes, standard Base64.
    /// First receipt: input = kassenId. Subsequent: input = previous compact JWS.
    /// </summary>
    public static string Compute(string? previousCompactJws, string kassenId)
    {
        var input = string.IsNullOrEmpty(previousCompactJws)
            ? kassenId
            : previousCompactJws;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var extracted = new byte[RksvSuite.ChainingBytesExtracted];
        Buffer.BlockCopy(hash, 0, extracted, 0, RksvSuite.ChainingBytesExtracted);
        return Convert.ToBase64String(extracted);
    }
}
