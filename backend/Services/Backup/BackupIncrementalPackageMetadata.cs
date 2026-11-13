using System.Text.Json;
using System.Text.Json.Nodes;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Reads/writes incremental package fields on <see cref="Models.Backup.BackupRun.ConfigSnapshotJson"/>
/// without breaking older snapshot schema versions.
/// </summary>
public static class BackupIncrementalPackageMetadata
{
    public const string PackageKindProperty = "packageKind";
    public const string IncrementalSinceUtcProperty = "incrementalSinceUtc";
    public const string PackageKindIncremental = "incremental";
    public const string PackageKindFull = "full";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string MergeIntoConfigSnapshot(string configSnapshotJson, DateTime incrementalSinceUtc)
    {
        var since = DateTime.SpecifyKind(incrementalSinceUtc, DateTimeKind.Utc);
        JsonNode root;
        try
        {
            root = JsonNode.Parse(configSnapshotJson) ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        if (root is not JsonObject obj)
            obj = new JsonObject();

        obj[PackageKindProperty] = PackageKindIncremental;
        obj[IncrementalSinceUtcProperty] = since;
        return obj.ToJsonString(JsonOptions);
    }

    public static bool TryReadIncrementalSinceUtc(string? configSnapshotJson, out DateTime sinceUtc)
    {
        sinceUtc = default;
        if (string.IsNullOrWhiteSpace(configSnapshotJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(configSnapshotJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty(PackageKindProperty, out var kindEl))
                return false;

            var kind = kindEl.GetString();
            if (!string.Equals(kind, PackageKindIncremental, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!root.TryGetProperty(IncrementalSinceUtcProperty, out var sinceEl))
                return false;

            if (sinceEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(
                    sinceEl.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                sinceUtc = DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);
                return true;
            }

            if (sinceEl.ValueKind == JsonValueKind.String)
                return false;

            // System.Text.Json may serialize DateTime as string already; also accept ISO via GetDateTime.
            sinceUtc = DateTime.SpecifyKind(sinceEl.GetDateTime().ToUniversalTime(), DateTimeKind.Utc);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
