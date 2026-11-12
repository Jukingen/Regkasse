using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KasseAPI_Final.Services.DataDeletion;

/// <summary>
/// Strips names, emails, phones, and addresses from audit JSON and free text.
/// Leaves amounts and other non-identity values in place.
/// </summary>
internal static partial class GdprPayloadRedactor
{
    public const string Redacted = "[redacted]";

    private static readonly HashSet<string> IdentityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "email",
        "firstname",
        "lastname",
        "surname",
        "name",
        "username",
        "phone",
        "phonenumber",
        "address",
        "customername",
        "customeremail",
        "customerphone",
        "customeraddress",
        "iban",
        "taxnumber",
        "notes",
        "actorname",
        "actor_name",
    };

    public static string? RedactJson(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return payload;

        try
        {
            var node = JsonNode.Parse(payload);
            if (node == null)
                return RedactFreeText(payload);
            RedactNode(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return RedactFreeText(payload);
        }
    }

    public static string? RedactFreeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (value.Contains('@', StringComparison.Ordinal))
            return EmailPattern().Replace(value, Redacted);
        return value;
    }

    private static void RedactNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var prop in obj.ToList())
                {
                    if (IdentityKeys.Contains(prop.Key))
                        obj[prop.Key] = Redacted;
                    else
                        RedactNode(prop.Value);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                    RedactNode(item);
                break;
            case JsonValue value when value.TryGetValue<string>(out var text) && text.Contains('@', StringComparison.Ordinal):
                value.ReplaceWith(EmailPattern().Replace(text, Redacted));
                break;
        }
    }

    [GeneratedRegex(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
}
