using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services;

/// <summary>Redacts secrets from Fiskaly request/response JSON before returning them to FA.</summary>
public static class FiskalyPayloadSanitizer
{
    public const string Redacted = "***";

    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "apikey",
        "api_key",
        "apisecret",
        "api_secret",
        "password",
        "token",
        "accesstoken",
        "access_token",
        "refreshtoken",
        "refresh_token",
        "authorization",
        "secret",
        "clientsecret",
        "client_secret",
        "bearertoken",
        "bearer_token"
    };

    public static string? SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is null)
                return json;
            RedactNode(node);
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch (JsonException)
        {
            return SanitizePlain(json);
        }
    }

    public static string? ExtractStackTrace(string? responseJson, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(responseJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                if (TryReadStack(doc.RootElement, out var stack))
                    return Truncate(stack);
            }
            catch (JsonException)
            {
                // Fall through to error-message heuristic.
            }
        }

        if (LooksLikeStack(errorMessage))
            return Truncate(errorMessage);

        return null;
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (IsSensitive(property.Key))
                {
                    obj[property.Key] = Redacted;
                    continue;
                }

                if (property.Value is not null)
                    RedactNode(property.Value);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                    RedactNode(item);
            }
        }
    }

    private static bool IsSensitive(string key)
    {
        var compact = key.Replace("_", string.Empty).Replace("-", string.Empty);
        return SensitiveKeys.Contains(key) || SensitiveKeys.Contains(compact);
    }

    private static string SanitizePlain(string value)
    {
        var trimmed = value.Trim();
        return trimmed
            .Replace("api_secret", Redacted, StringComparison.OrdinalIgnoreCase)
            .Replace("api_key", Redacted, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadStack(JsonElement element, out string? stack)
    {
        stack = null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("stackTrace")
                    || property.NameEquals("stack_trace")
                    || property.NameEquals("stack")
                    || property.NameEquals("exceptionStackTrace"))
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        stack = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(stack))
                            return true;
                    }
                }

                if (TryReadStack(property.Value, out stack) && !string.IsNullOrWhiteSpace(stack))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryReadStack(item, out stack) && !string.IsNullOrWhiteSpace(stack))
                    return true;
            }
        }

        return false;
    }

    private static bool LooksLikeStack(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Contains(" at ", StringComparison.Ordinal)
        && (message.Contains(".cs:line", StringComparison.OrdinalIgnoreCase)
            || message.Contains(":line ", StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        const int max = 8000;
        return value.Length <= max ? value : value[..max] + "…";
    }
}
