using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services;

public static class OfflinePayloadHashing
{
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
                if (item == null) newArr.Add((JsonNode?)null);
                else newArr.Add(NormalizeNode(item));
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

