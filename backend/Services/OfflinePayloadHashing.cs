using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services;

public static class OfflinePayloadHashing
{
    /// <summary>
    /// True when stored hash equals SHA256(UTF8) of runtime-canonical JSON (sorted keys).
    /// Migration backfill used digest(PayloadJson::text) without key sorting — often differs.
    /// </summary>
    public static bool StoredHashMatchesRuntimeCanonical(string? payloadJson, string? storedHashHex)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || string.IsNullOrWhiteSpace(storedHashHex))
            return string.IsNullOrWhiteSpace(storedHashHex) && string.IsNullOrWhiteSpace(payloadJson);
        var (_, h) = NormalizeAndHash(payloadJson);
        return string.Equals(h, storedHashHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Runtime canonical hash only (for SQL-free comparison in maintenance).</summary>
    public static string ComputeRuntimeCanonicalHashHex(string payloadJson)
    {
        var (_, h) = NormalizeAndHash(payloadJson);
        return h;
    }

    public static (string NormalizedJson, string Sha256Hex) NormalizeAndHash(string payloadJson)
    {
        // Normalize by sorting object keys recursively so semantically identical payloads hash identically.
        var node = JsonNode.Parse(payloadJson);
        if (node == null)
            return ("{}", Sha256HexOfString("{}"));

        var normalizedNode = NormalizeNode(node);
        var normalizedJson = normalizedNode.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var hash = Sha256HexOfString(normalizedJson);
        return (normalizedJson, hash);
    }

    private static JsonNode NormalizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var newObj = new JsonObject();
            foreach (var key in keys)
            {
                var val = obj[key];
                if (val == null)
                {
                    newObj[key] = null;
                }
                else
                {
                    newObj[key] = NormalizeNode(val);
                }
            }
            return newObj;
        }

        if (node is JsonArray arr)
        {
            var newArr = new JsonArray();
            foreach (var item in arr)
            {
                if (item == null)
                    newArr.Add((JsonNode?)null);
                else
                    newArr.Add(NormalizeNode(item));
            }
            return newArr;
        }

        // JsonValue: keep as-is.
        // Important: JsonNode instances are single-parent. We must detach by re-parsing.
        return JsonNode.Parse(node.ToJsonString()) ?? node;
    }

    private static string Sha256HexOfString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

