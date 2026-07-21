using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.DataProtection;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV/GDPR: offline intent JSON must not keep Mehrzweckgutschein plaintext in <see cref="OfflineTransaction.PayloadJson"/>.
/// Full canonical payload is DP-protected server-side; redacted canonical JSON remains in PayloadJson for storage and admins.
/// </summary>
public static class OfflineVoucherPayloadProtector
{
    public const string Purpose = "Regkasse.OfflinePayment.FullPayload.v1";

    /// <summary>Outcome of sealing an incoming offline replay payload.</summary>
    public sealed record PrepareResult(string StoredPayloadJson, string PayloadHashHex, string? ProtectedBase64);

    public static IDataProtector CreateProtector(IDataProtectionProvider provider) =>
        provider.CreateProtector(Purpose);

    /// <summary>Backward-compatible overload without optional field AES layer.</summary>
    public static PrepareResult PrepareForPersistence(string payloadRaw, IDataProtector protector) =>
        PrepareForPersistence(payloadRaw, protector, voucherFieldAesKey: null);

    /// <summary>
    /// If the normalized payload carries voucher plaintext, encrypt the full canonical JSON and persist a redacted copy.
    /// Otherwise store plaintext JSON unchanged (backward compatible non-voucher payments).
    /// When <paramref name="voucherFieldAesKey"/> is set, voucher code strings are AES-wrapped inside the UTF-8 blob before Data Protection (hash remains on plaintext-normalized JSON).
    /// </summary>
    public static PrepareResult PrepareForPersistence(string payloadRaw, IDataProtector protector, byte[]? voucherFieldAesKey)
    {
        var (normalizedFull, hash) = OfflinePayloadHashing.NormalizeAndHash(payloadRaw);
        if (!NormalizedPayloadContainsVoucherPlainSecrets(normalizedFull))
            return new PrepareResult(normalizedFull, hash, ProtectedBase64: null);

        var normalizedForProtect = voucherFieldAesKey != null
            ? OfflineVoucherFieldAesEncryption.EncryptVoucherPlaintextFieldsInNormalizedJson(normalizedFull, voucherFieldAesKey)
            : normalizedFull;

        var secretBytes = Encoding.UTF8.GetBytes(normalizedForProtect);
        var enc = protector.Protect(secretBytes);
        var redacted = RedactVoucherSecretsInNormalizedJson(normalizedFull);
        var (redactedNorm, _) = OfflinePayloadHashing.NormalizeAndHash(redacted);
        return new PrepareResult(redactedNorm, hash, Convert.ToBase64String(enc));
    }

    /// <summary>Full canonical JSON suitable for hashing or deserializing to CreatePaymentRequest.</summary>
    public static string ResolveNormalizedPayloadJson(
        string payloadJson,
        string? payloadSecretsProtected,
        IDataProtector? protector) =>
        ResolveNormalizedPayloadJson(payloadJson, payloadSecretsProtected, protector, voucherFieldAesKey: null);

    /// <inheritdoc cref="ResolveNormalizedPayloadJson(string,string?,IDataProtector?)"/>
    public static string ResolveNormalizedPayloadJson(
        string payloadJson,
        string? payloadSecretsProtected,
        IDataProtector? protector,
        byte[]? voucherFieldAesKey)
    {
        if (string.IsNullOrEmpty(payloadSecretsProtected))
            return string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;

        if (protector == null)
            throw new InvalidOperationException(
                "Offline transaction payload_secrets_protected is set but IDataProtectionProvider is not available; cannot unwrap voucher replay payload.");

        var raw = protector.Unprotect(Convert.FromBase64String(payloadSecretsProtected));
        var json = Encoding.UTF8.GetString(raw);
        if (voucherFieldAesKey != null && json.Contains(OfflineVoucherFieldAesEncryption.EncryptedValuePrefix, StringComparison.Ordinal))
            json = OfflineVoucherFieldAesEncryption.DecryptVoucherPlaintextFieldsInNormalizedJson(json, voucherFieldAesKey);
        return json;
    }

    public static string ResolveNormalizedPayloadJson(OfflineTransaction row, IDataProtector protector, byte[]? voucherFieldAesKey = null) =>
        ResolveNormalizedPayloadJson(row.PayloadJson, row.PayloadSecretsProtected, protector, voucherFieldAesKey);

    private static bool TryGetPropertyIgnoreCase(JsonElement parent, string name, out JsonElement value)
    {
        foreach (var p in parent.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool NormalizedPayloadContainsVoucherPlainSecrets(string normalizedPayloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(normalizedPayloadJson);
            if (!TryGetPropertyIgnoreCase(doc.RootElement, "payment", out var pay) ||
                pay.ValueKind != JsonValueKind.Object)
                return false;

            if (TryGetPropertyIgnoreCase(pay, "voucherCode", out var single) &&
                single.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(single.GetString()))
                return true;

            if (!TryGetPropertyIgnoreCase(pay, "voucherRedemptions", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                    continue;
                if (TryGetPropertyIgnoreCase(el, "code", out var c) &&
                    c.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(c.GetString()))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Remove voucher plaintext from canonical JSON while keeping monetary structure fields.</summary>
    private static string? FindObjectKeyIgnoreCase(JsonObject obj, string name) =>
        obj.Select(kvp => kvp.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));

    private static string RedactVoucherSecretsInNormalizedJson(string normalizedPayloadJson)
    {
        if (JsonNode.Parse(normalizedPayloadJson) is not JsonObject rootObj)
            return normalizedPayloadJson;

        var payKey = FindObjectKeyIgnoreCase(rootObj, "payment");
        if (payKey == null || rootObj[payKey] is not JsonObject pay)
            return normalizedPayloadJson;

        var voucherCodeKey = FindObjectKeyIgnoreCase(pay, "voucherCode");
        if (voucherCodeKey != null)
            pay[voucherCodeKey] = string.Empty;

        var redemptionsKey = FindObjectKeyIgnoreCase(pay, "voucherRedemptions");
        if (redemptionsKey != null && pay[redemptionsKey] is JsonArray ra)
        {
            foreach (var node in ra)
            {
                if (node is JsonObject line)
                {
                    var codeKey = FindObjectKeyIgnoreCase(line, "code");
                    if (codeKey != null)
                        line[codeKey] = string.Empty;
                }
            }
        }

        return rootObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
