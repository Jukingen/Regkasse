using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.VerticalProfiles;

public static class VerticalProfileSources
{
    public const string Seed = "seed";
    public const string Override = "override";
    public const string Custom = "custom";
}

public static class VerticalProfileFeatureKeys
{
    public const string Tables = "tables";
    public const string KitchenDisplay = "kitchenDisplay";
    public const string PatientRecord = "patientRecord";
    public const string ServiceDuration = "serviceDuration";
    public const string Appointment = "appointment";
    public const string ImeiTracking = "imeiTracking";
    public const string RouteTracking = "routeTracking";
    public const string RoomTracking = "roomTracking";
    public const string TicketScan = "ticketScan";

    public static readonly IReadOnlyList<string> All =
    [
        Tables,
        KitchenDisplay,
        PatientRecord,
        ServiceDuration,
        Appointment,
        ImeiTracking,
        RouteTracking,
        RoomTracking,
        TicketScan,
    ];
}

public static class VerticalProfileCatalogErrorCodes
{
    public const string ProfileNotFound = "VERTICAL_PROFILE_NOT_FOUND";
    public const string ProfileExists = "PROFILE_EXISTS";
    public const string ProfileInUse = "PROFILE_IN_USE";
    public const string ProfileIsSeed = "PROFILE_IS_SEED";
    public const string InvalidProfile = "INVALID_VERTICAL_PROFILE";
    public const string IdImmutable = "PROFILE_ID_IMMUTABLE";
}

/// <summary>Shared JSON shape checks for vertical-profile catalogs and tenant overlays.</summary>
public static partial class VerticalProfileJson
{
    public const int MaxJsonLength = 64 * 1024;
    public const string DefaultFieldsJson = """{"customer":[],"product":[],"order":[]}""";

    private static readonly Regex SlugRegex = SlugPattern();

    public static string DefaultFeaturesJson()
    {
        var features = new JsonObject();
        foreach (var key in VerticalProfileFeatureKeys.All)
            features[key] = false;
        return features.ToJsonString();
    }

    public static bool TryNormalizeId(string? raw, out string id)
    {
        id = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        return id.Length is >= 2 and <= 64 && SlugRegex.IsMatch(id);
    }

    public static string NormalizeFeatures(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return DefaultFeaturesJson();

        var node = ParseObject(element, "posFeatures");
        ValidatePosFeatures(node);
        return node.ToJsonString();
    }

    public static string NormalizeFields(JsonElement element, string propertyName)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return DefaultFieldsJson;

        var node = ParseObject(element, propertyName);
        ValidateFieldLists(propertyName, node);
        return node.ToJsonString();
    }

    public static string NormalizeLayout(string? layout)
    {
        var value = layout?.Trim() ?? string.Empty;
        if (!VerticalProfileLayouts.All.Contains(value))
        {
            throw new ArgumentException(
                $"posLayout must be one of: {string.Join(", ", VerticalProfileLayouts.All)}.");
        }

        return value;
    }

    public static void ValidatePosFeatures(JsonNode? value)
    {
        if (value is not JsonObject features)
            throw new ArgumentException("posFeatures must be a JSON object.");

        foreach (var feature in features)
        {
            if (feature.Value is not JsonValue jsonValue || !jsonValue.TryGetValue<bool>(out _))
                throw new ArgumentException($"posFeatures.{feature.Key} must be a boolean.");
        }
    }

    public static void ValidateFieldLists(string propertyName, JsonNode? value)
    {
        if (value is not JsonObject fieldGroups)
            throw new ArgumentException($"{propertyName} must be a JSON object.");

        foreach (var group in fieldGroups)
        {
            if (group.Key is not ("customer" or "product" or "order"))
                throw new ArgumentException($"Unknown {propertyName} group '{group.Key}'.");
            if (group.Value is not JsonArray fields)
                throw new ArgumentException($"{propertyName}.{group.Key} must be an array.");

            foreach (var field in fields)
            {
                if (field is not JsonValue jsonValue
                    || !jsonValue.TryGetValue<string>(out var fieldName)
                    || string.IsNullOrWhiteSpace(fieldName))
                {
                    throw new ArgumentException($"{propertyName}.{group.Key} must contain non-empty strings.");
                }
            }
        }
    }

    public static int CountEnabledFeatures(string json)
    {
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) is not JsonObject features)
            return 0;

        var count = 0;
        foreach (var feature in features)
        {
            if (feature.Value is JsonValue jsonValue
                && jsonValue.TryGetValue<bool>(out var enabled)
                && enabled)
            {
                count++;
            }
        }

        return count;
    }

    public static IReadOnlyList<string> RemovedFeatureKeys(string previousJson, string nextJson)
    {
        var previous = ReadBoolMap(previousJson);
        var next = ReadBoolMap(nextJson);
        var removed = new List<string>();
        foreach (var (key, wasEnabled) in previous)
        {
            if (!wasEnabled)
                continue;
            if (!next.TryGetValue(key, out var stillEnabled) || !stillEnabled)
                removed.Add(key);
        }

        return removed;
    }

    public static int CountTopLevelKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return 0;
        return JsonNode.Parse(json) is JsonObject node ? node.Count : 0;
    }

    private static Dictionary<string, bool> ReadBoolMap(string json)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) is not JsonObject features)
            return map;

        foreach (var feature in features)
        {
            if (feature.Value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var enabled))
                map[feature.Key] = enabled;
        }

        return map;
    }

    private static JsonObject ParseObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{propertyName} must be a JSON object.");

        var raw = element.GetRawText();
        if (raw.Length > MaxJsonLength)
            throw new ArgumentException($"{propertyName} must not exceed {MaxJsonLength} characters.");

        return JsonNode.Parse(raw) as JsonObject
            ?? throw new ArgumentException($"{propertyName} must be a JSON object.");
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
