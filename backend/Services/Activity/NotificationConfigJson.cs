using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

internal static class NotificationConfigJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    public static string Serialize(NotificationConfig config) =>
        JsonSerializer.Serialize(config, Options);

    public static NotificationConfig Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? NotificationConfig.CreateDefault()
            : JsonSerializer.Deserialize<NotificationConfig>(json, Options) ?? NotificationConfig.CreateDefault();
}
