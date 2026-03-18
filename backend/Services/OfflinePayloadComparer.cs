using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Structural JSON equality for immutable offline payload verification.
    /// </summary>
    public static class OfflinePayloadComparer
    {
        public static bool EqualsNormalized(string? storedJson, string? incomingJson)
        {
            if (string.IsNullOrWhiteSpace(storedJson) && string.IsNullOrWhiteSpace(incomingJson))
                return true;
            if (string.IsNullOrWhiteSpace(storedJson) || string.IsNullOrWhiteSpace(incomingJson))
                return false;

            try
            {
                var a = JsonNode.Parse(storedJson);
                var b = JsonNode.Parse(incomingJson);
                return JsonNode.DeepEquals(a, b);
            }
            catch (JsonException)
            {
                return string.Equals(storedJson.Trim(), incomingJson.Trim(), StringComparison.Ordinal);
            }
        }
    }
}
