using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services.RestoreVerification;

internal static class ManualRestoreRunDetailsJson
{
    public const string ManualRestoreRequestIdKey = "manualRestoreRequestId";
    public const string TargetDatabaseNameKey = "targetDatabaseName";
    public const string ValidationOnlyKey = "validationOnly";

    public static string Build(Guid manualRestoreRequestId, string targetDatabaseName)
    {
        var node = new JsonObject
        {
            [ManualRestoreRequestIdKey] = manualRestoreRequestId.ToString(),
            [TargetDatabaseNameKey] = targetDatabaseName,
            [ValidationOnlyKey] = true,
            ["scope"] = "manual_restore_validation_only"
        };
        return node.ToJsonString();
    }

    public static string? TryGetTargetDatabaseName(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty(TargetDatabaseNameKey, out var el)
                && el.ValueKind == JsonValueKind.String)
            {
                var name = el.GetString();
                return string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static Guid? TryGetManualRestoreRequestId(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            if (doc.RootElement.TryGetProperty(ManualRestoreRequestIdKey, out var el)
                && el.ValueKind == JsonValueKind.String
                && Guid.TryParse(el.GetString(), out var id))
                return id;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    public static bool IsValidationOnlyManualRestore(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            return doc.RootElement.TryGetProperty(ValidationOnlyKey, out var el)
                   && el.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
