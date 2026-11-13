using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services;

/// <summary>
/// Defense-in-depth: encrypt Gutschein codes at rest inside the UTF-8 blob that is sealed by Data Protection.
/// Wire format prefix <c>rk-aes1:</c> marks ciphertext; legacy rows and non-configured keys skip this layer.
/// </summary>
public static class OfflineVoucherFieldAesEncryption
{
    public const string EncryptedValuePrefix = "rk-aes1:";
    private const string Prefix = EncryptedValuePrefix;

    public static string EncryptVoucherCode(string voucherCode, ReadOnlySpan<byte> key)
    {
        if (string.IsNullOrEmpty(voucherCode))
            return voucherCode;
        if (voucherCode.StartsWith(Prefix, StringComparison.Ordinal))
            return voucherCode;

        using var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(voucherCode);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var payload = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.AsSpan().CopyTo(payload);
        cipherBytes.AsSpan().CopyTo(payload.AsSpan(aes.IV.Length));
        return Prefix + Convert.ToBase64String(payload);
    }

    public static string DecryptVoucherCode(string stored, ReadOnlySpan<byte> key)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;

        var raw = Convert.FromBase64String(stored[Prefix.Length..]);
        if (raw.Length <= 16)
            throw new CryptographicException("Invalid offline voucher AES payload (too short).");

        var iv = raw.AsSpan(0, 16);

        _ = raw.AsSpan(16);
        using var aes = Aes.Create();
        aes.Key = key.ToArray();
        aes.IV = iv.ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var cipherArr = raw[16..];
        var plain = decryptor.TransformFinalBlock(cipherArr, 0, cipherArr.Length);
        return Encoding.UTF8.GetString(plain);
    }

    /// <summary>Encrypt voucher string fields in normalized payment JSON (hash must be computed before this transform).</summary>
    public static string EncryptVoucherPlaintextFieldsInNormalizedJson(string normalizedPayloadJson, byte[] key)
    {
        if (JsonNode.Parse(normalizedPayloadJson) is not JsonObject rootObj)
            return normalizedPayloadJson;

        var payKey = FindObjectKeyIgnoreCase(rootObj, "payment");
        if (payKey == null || rootObj[payKey] is not JsonObject pay)
            return normalizedPayloadJson;

        var voucherCodeKey = FindObjectKeyIgnoreCase(pay, "voucherCode");
        if (voucherCodeKey != null &&
            pay[voucherCodeKey] is JsonValue vc &&
            vc.TryGetValue<string>(out var single) &&
            !string.IsNullOrWhiteSpace(single))
            pay[voucherCodeKey] = EncryptVoucherCode(single, key);

        var redemptionsKey = FindObjectKeyIgnoreCase(pay, "voucherRedemptions");
        if (redemptionsKey != null && pay[redemptionsKey] is JsonArray ra)
        {
            foreach (var node in ra)
            {
                if (node is not JsonObject line)
                    continue;
                var codeKey = FindObjectKeyIgnoreCase(line, "code");
                if (codeKey != null &&
                    line[codeKey] is JsonValue cv &&
                    cv.TryGetValue<string>(out var code) &&
                    !string.IsNullOrWhiteSpace(code))
                    line[codeKey] = EncryptVoucherCode(code, key);
            }
        }

        return rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static string DecryptVoucherPlaintextFieldsInNormalizedJson(string normalizedPayloadJson, byte[] key)
    {
        if (JsonNode.Parse(normalizedPayloadJson) is not JsonObject rootObj)
            return normalizedPayloadJson;

        var payKey = FindObjectKeyIgnoreCase(rootObj, "payment");
        if (payKey == null || rootObj[payKey] is not JsonObject pay)
            return normalizedPayloadJson;

        var voucherCodeKey = FindObjectKeyIgnoreCase(pay, "voucherCode");
        if (voucherCodeKey != null &&
            pay[voucherCodeKey] is JsonValue vc &&
            vc.TryGetValue<string>(out var single) &&
            single.StartsWith(Prefix, StringComparison.Ordinal))
            pay[voucherCodeKey] = DecryptVoucherCode(single, key);

        var redemptionsKey = FindObjectKeyIgnoreCase(pay, "voucherRedemptions");
        if (redemptionsKey != null && pay[redemptionsKey] is JsonArray ra)
        {
            foreach (var node in ra)
            {
                if (node is not JsonObject line)
                    continue;
                var codeKey = FindObjectKeyIgnoreCase(line, "code");
                if (codeKey != null &&
                    line[codeKey] is JsonValue cv &&
                    cv.TryGetValue<string>(out var code) &&
                    code.StartsWith(Prefix, StringComparison.Ordinal))
                    line[codeKey] = DecryptVoucherCode(code, key);
            }
        }

        return rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string? FindObjectKeyIgnoreCase(JsonObject obj, string name) =>
        obj.Select(kvp => kvp.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
}
