using System.Text;
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

    /// <summary>
    /// If the normalized payload carries voucher plaintext, encrypt the full canonical JSON and persist a redacted copy.
    /// Otherwise store plaintext JSON unchanged (backward compatible non-voucher payments).
    /// </summary>
    public static PrepareResult PrepareForPersistence(string payloadRaw, IDataProtector protector)
    {
        var (normalizedFull, hash) = OfflinePayloadHashing.NormalizeAndHash(payloadRaw);
        if (!NormalizedPayloadContainsVoucherPlainSecrets(normalizedFull))
            return new PrepareResult(normalizedFull, hash, ProtectedBase64: null);

        var secretBytes = Encoding.UTF8.GetBytes(normalizedFull);
        var enc = protector.Protect(secretBytes);
        var redacted = RedactVoucherSecretsInNormalizedJson(normalizedFull);
        var (redactedNorm, _) = OfflinePayloadHashing.NormalizeAndHash(redacted);
        return new PrepareResult(redactedNorm, hash, Convert.ToBase64String(enc));
    }

    /// <summary>Full canonical JSON suitable for hashing or deserializing to CreatePaymentRequest.</summary>
    public static string ResolveNormalizedPayloadJson(
        string payloadJson,
        string? payloadSecretsProtected,
        IDataProtector? protector)
    {
        if (string.IsNullOrEmpty(payloadSecretsProtected))
            return string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;

        if (protector == null)
            throw new InvalidOperationException(
                "Offline transaction payload_secrets_protected is set but IDataProtectionProvider is not available; cannot unwrap voucher replay payload.");

        var raw = protector.Unprotect(Convert.FromBase64String(payloadSecretsProtected));
        return Encoding.UTF8.GetString(raw);
    }

    public static string ResolveNormalizedPayloadJson(OfflineTransaction row, IDataProtector protector) =>
        ResolveNormalizedPayloadJson(row.PayloadJson, row.PayloadSecretsProtected, protector);

    private static bool NormalizedPayloadContainsVoucherPlainSecrets(string normalizedPayloadJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(normalizedPayloadJson);
            if (!doc.RootElement.TryGetProperty("payment", out var pay))
                return false;

            if (pay.TryGetProperty("voucherCode", out var single) &&
                single.ValueKind == System.Text.Json.JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(single.GetString()))
                return true;

            if (!pay.TryGetProperty("voucherRedemptions", out var arr) ||
                arr.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;

            foreach (var el in arr.EnumerateArray())
            {
                if (el.TryGetProperty("code", out var c) &&
                    c.ValueKind == System.Text.Json.JsonValueKind.String &&
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
    private static string RedactVoucherSecretsInNormalizedJson(string normalizedPayloadJson)
    {
        if (JsonNode.Parse(normalizedPayloadJson) is not JsonObject rootObj)
            return normalizedPayloadJson;

        if (rootObj["payment"] is not JsonObject pay)
            return normalizedPayloadJson;

        pay["voucherCode"] = string.Empty;

        if (pay["voucherRedemptions"] is JsonArray ra)
        {
            foreach (var node in ra)
            {
                if (node is JsonObject line)
                    line["code"] = string.Empty;
            }
        }

        return rootObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });
    }
}
