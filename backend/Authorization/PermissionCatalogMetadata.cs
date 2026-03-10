using System.Collections.Frozen;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Metadata for each permission in the catalog: key, group, resource, action, optional description.
/// Used by GET /api/UserManagement/roles/permissions-catalog.
/// </summary>
public static class PermissionCatalogMetadata
{
    /// <summary>
    /// Resource name -> display group (for UI grouping).
    /// </summary>
    private static readonly FrozenDictionary<string, string> ResourceToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["user"] = "User & Role",
        ["role"] = "User & Role",
        ["product"] = "Product",
        ["category"] = "Product",
        ["modifier"] = "Product",
        ["order"] = "Order & Sale",
        ["table"] = "Order & Sale",
        ["cart"] = "Order & Sale",
        ["sale"] = "Order & Sale",
        ["payment"] = "Payment",
        ["refund"] = "Payment",
        ["discount"] = "Payment",
        ["cashregister"] = "Cash & Shift",
        ["cashdrawer"] = "Cash & Shift",
        ["shift"] = "Cash & Shift",
        ["inventory"] = "Inventory",
        ["customer"] = "Customer",
        ["invoice"] = "Invoice",
        ["creditnote"] = "Invoice",
        ["settings"] = "Settings",
        ["localization"] = "Settings",
        ["receipttemplate"] = "Settings",
        ["audit"] = "Audit & Report",
        ["report"] = "Audit & Report",
        ["finanzonline"] = "FinanzOnline",
        ["kitchen"] = "Kitchen",
        ["tse"] = "TSE",
        ["system"] = "System",
        ["price"] = "Legacy",
        ["receipt"] = "Legacy",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public sealed record Item(string Key, string Group, string Resource, string Action, string? Description);

    /// <summary>
    /// Returns metadata for all permissions in PermissionCatalog.All.
    /// Key format: "resource.action". Group from resource; description optional.
    /// </summary>
    public static IReadOnlyList<Item> GetAll()
    {
        var list = new List<Item>();
        foreach (var key in PermissionCatalog.All)
        {
            var (resource, action) = ParseKey(key);
            var group = ResourceToGroup.TryGetValue(resource, out var g) ? g : "Other";
            var description = GetDescription(key);
            list.Add(new Item(key, group, resource, action, description));
        }
        return list;
    }

    private static (string resource, string action) ParseKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return ("", "");
        var i = key.IndexOf('.');
        if (i <= 0) return (key, "");
        return (key[..i], key[(i + 1)..]);
    }

    private static string? GetDescription(string key)
    {
        // Optional: return null or a short description for known keys. Keep minimal for now.
        return null;
    }

    /// <summary>
    /// Returns true if the permission key is in the catalog (valid for assignment).
    /// </summary>
    public static bool IsValidPermissionKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        return PermissionCatalog.All.Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}
