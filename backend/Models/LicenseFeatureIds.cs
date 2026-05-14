using System.Text.Json;

namespace KasseAPI_Final.Models;

/// <summary>Stable string identifiers for licensed product surfaces (POS + Frontend Admin).</summary>
public static class LicenseFeatureIds
{
    public const string PosFiscal = "pos_fiscal";

    public const string PosOffline = "pos_offline";

    public const string AdminBasic = "admin_basic";

    public const string AdminRksv = "admin_rksv";

    public const string AdminLicenseManage = "admin_license_manage";

    /// <summary>Default paid / trial entitlement: full single-license bundle.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        PosFiscal,
        PosOffline,
        AdminBasic,
        AdminRksv,
        AdminLicenseManage,
    ];

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        PosFiscal,
        PosOffline,
        AdminBasic,
        AdminRksv,
        AdminLicenseManage,
    };

    /// <summary>Returns null when all entries are valid; otherwise an English validation message.</summary>
    public static string? ValidateRequestedFeatures(IReadOnlyList<string>? requested)
    {
        if (requested is null || requested.Count == 0)
            return null;
        foreach (var id in requested)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "Feature id must not be empty.";
            if (!Known.Contains(id.Trim()))
                return $"Unknown license feature: {id.Trim()}";
        }

        return null;
    }

    /// <summary>Distinct normalized ids (known-only; used after validation).</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> requested)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in requested)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var t = id.Trim();
            if (Known.Contains(t))
                set.Add(t);
        }

        return set.Count == 0 ? All : set.OrderBy(s => s, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Null when JSON is missing/blank/invalid; otherwise a normalized non-empty subset of known ids.</summary>
    public static IReadOnlyList<string>? TryParseStoredFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;
            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.String)
                    continue;
                var s = el.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    continue;
                var t = s.Trim();
                if (Known.Contains(t))
                    list.Add(t);
            }

            return list.Count == 0 ? null : Normalize(list);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses JSON array of strings from DB or API; unknown entries are ignored.</summary>
    public static IReadOnlyList<string> ParseJsonArrayOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return All;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return All;
            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s) && Known.Contains(s))
                        list.Add(s);
                }
            }

            return list.Count == 0 ? All : Normalize(list);
        }
        catch
        {
            return All;
        }
    }

    public static string SerializeJsonArray(IReadOnlyList<string> features) =>
        JsonSerializer.Serialize(features.ToArray());
}
