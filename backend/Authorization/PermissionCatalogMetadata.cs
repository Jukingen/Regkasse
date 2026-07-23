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
    /// Names align with FA sidebar IA; slugs drive <c>users.roleDrawer.groups.*</c> i18n keys.
    /// </summary>
    private static readonly FrozenDictionary<string, string> ResourceToGroup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Mitarbeiter (sidebar: Staff / Access)
        ["user"] = "Mitarbeiter",
        ["role"] = "Mitarbeiter",
        // Kassenverwaltung (sidebar: Cash registers / shifts)
        ["cashregister"] = "Kassenverwaltung",
        ["cash_register"] = "Kassenverwaltung",
        ["cashdrawer"] = "Kassenverwaltung",
        ["shift"] = "Kassenverwaltung",
        // Bestellung & Verkauf (orders, tables, invoices, kitchen)
        ["order"] = "Bestellung & Verkauf",
        ["table"] = "Bestellung & Verkauf",
        ["cart"] = "Bestellung & Verkauf",
        ["sale"] = "Bestellung & Verkauf",
        ["invoice"] = "Bestellung & Verkauf",
        ["creditnote"] = "Bestellung & Verkauf",
        ["kitchen"] = "Bestellung & Verkauf",
        // Zahlung (payments, refunds, discounts, vouchers)
        ["payment"] = "Zahlung",
        ["refund"] = "Zahlung",
        ["discount"] = "Zahlung",
        ["voucher"] = "Zahlung",
        // Sortiment & Preise
        ["product"] = "Sortiment & Preise",
        ["category"] = "Sortiment & Preise",
        ["modifier"] = "Sortiment & Preise",
        // Lager
        ["inventory"] = "Lager",
        // Kunden & Vorteile
        ["customer"] = "Kunden & Vorteile",
        ["benefit"] = "Kunden & Vorteile",
        // RKSV & FinanzOnline (TSE + special receipts merged here)
        ["finanzonline"] = "RKSV & FinanzOnline",
        ["tse"] = "RKSV & FinanzOnline",
        ["rksv"] = "RKSV & FinanzOnline",
        // Tagesabschluss (sidebar: Betrieb)
        ["daily-closing"] = "Tagesabschluss",
        // Audit & Berichte
        ["audit"] = "Audit & Berichte",
        ["report"] = "Audit & Berichte",
        // Backup & Disaster Recovery (sidebar backup hub; reads still gated by settings.view)
        ["backup"] = "Backup & Disaster Recovery",
        // Einstellungen
        ["settings"] = "Einstellungen",
        ["license"] = "Einstellungen",
        ["website"] = "Einstellungen",
        ["localization"] = "Einstellungen",
        ["receipttemplate"] = "Einstellungen",
        // Digitale Dienste / System / Other
        ["digital"] = "Digitale Dienste",
        ["system"] = "System",
        ["price"] = "Sonstige",
        ["receipt"] = "Sonstige",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Permission-key overrides when the resource prefix alone would place the key in the wrong UI group.
    /// Example: <c>settings.backup</c> belongs with Backup &amp; DR, not general Einstellungen.
    /// </summary>
    private static readonly FrozenDictionary<string, string> PermissionKeyToGroupOverride =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AppPermissions.SettingsBackup] = "Backup & Disaster Recovery",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Converts display group name to a stable slug for API (e.g. "Kassenverwaltung" -> "kassenverwaltung").
    /// Used by role capability matrix permission grouping.
    /// </summary>
    public static string GetGroupKey(string groupDisplayName)
    {
        if (string.IsNullOrWhiteSpace(groupDisplayName))
            return "other";
        var s = groupDisplayName.Trim()
            .Replace(" & ", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
        return string.IsNullOrEmpty(s) ? "other" : s.ToLowerInvariant();
    }

    private static readonly Lazy<FrozenDictionary<string, string>> PermissionKeyToGroupKey = new(BuildPermissionToGroupKey);

    private static FrozenDictionary<string, string> BuildPermissionToGroupKey()
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in GetAll())
        {
            var groupKey = GetGroupKey(item.Group);
            dict.TryAdd(item.Key, groupKey);
        }
        return dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the group key (slug) for a permission key, or "other" if unknown.
    /// Used to build permissionGroups in role capability matrix response.
    /// </summary>
    public static string GetGroupKeyForPermission(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            return "other";
        return PermissionKeyToGroupKey.Value.TryGetValue(permissionKey, out var key) ? key : "other";
    }

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
            var group = PermissionKeyToGroupOverride.TryGetValue(key, out var overrideGroup)
                ? overrideGroup
                : ResourceToGroup.TryGetValue(resource, out var g) ? g : "Other";
            var description = GetDescription(key);
            list.Add(new Item(key, group, resource, action, description));
        }
        return list;
    }

    private static (string resource, string action) ParseKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return ("", "");
        var i = key.IndexOf('.');
        if (i <= 0)
            return (key, "");
        return (key[..i], key[(i + 1)..]);
    }

    private static string? GetDescription(string key)
    {
        return key switch
        {
            AppPermissions.CashRegisterView => "Kassen anzeigen",
            AppPermissions.CashRegisterManage => "Kassen verwalten (erstellen, bearbeiten)",
            AppPermissions.CashRegisterDecommission => "Kassen stilllegen",
            AppPermissions.BackupManage => "Backups verwalten (manuell auslösen, Zeitplan bearbeiten)",
            AppPermissions.SettingsBackup => "Backup-Einstellungen (Legacy; bevorzugt backup.manage)",
            AppPermissions.DailyClosingView => "Tagesabschluss anzeigen",
            AppPermissions.DailyClosingExecute => "Tagesabschluss durchführen",
            AppPermissions.WebsiteManage => "Domain und Website-Anpassung verwalten",
            AppPermissions.DigitalView => "Digitale Dienste anzeigen (Status, URLs)",
            AppPermissions.DigitalPreview => "Digitale Dienste-Vorschau anzeigen",
            AppPermissions.DigitalRequest => "Digitale Dienste anfordern (Super-Admin-Freigabe)",
            AppPermissions.DigitalCreate => "Website/App erzeugen (Super-Admin)",
            AppPermissions.DigitalPublish => "Website/App veröffentlichen (Super-Admin)",
            AppPermissions.DigitalEdit => "Digitale Dienste bearbeiten (Super-Admin)",
            AppPermissions.DigitalDelete => "Digitale Dienste löschen (Super-Admin)",
            AppPermissions.DigitalWebView => "Web-Service anzeigen (Legacy; bevorzugt digital.view)",
            AppPermissions.DigitalWebPreview => "Website-Vorschau (Legacy; bevorzugt digital.preview)",
            AppPermissions.DigitalWebRequest => "Website-Anfrage (Legacy; bevorzugt digital.request)",
            AppPermissions.DigitalWebCreate => "Website erzeugen (Legacy; bevorzugt digital.create)",
            AppPermissions.DigitalWebPublish => "Website veröffentlichen (Legacy; bevorzugt digital.publish)",
            AppPermissions.DigitalWebDelete => "Website löschen (Legacy; bevorzugt digital.delete)",
            AppPermissions.DigitalWebUse => "Web-Service nutzen (Legacy; bevorzugt digital.create)",
            AppPermissions.DigitalAppView => "App-Service anzeigen (Legacy; bevorzugt digital.view)",
            AppPermissions.DigitalAppPreview => "App-Vorschau (Legacy; bevorzugt digital.preview)",
            AppPermissions.DigitalAppRequest => "App-Anfrage (Legacy; bevorzugt digital.request)",
            AppPermissions.DigitalAppCreate => "App erzeugen (Legacy; bevorzugt digital.create)",
            AppPermissions.DigitalAppPublish => "App veröffentlichen (Legacy; bevorzugt digital.publish)",
            AppPermissions.DigitalAppDelete => "App löschen (Legacy; bevorzugt digital.delete)",
            AppPermissions.DigitalAppUse => "App-Service nutzen (Legacy; bevorzugt digital.create)",
            AppPermissions.DigitalManage => "Digitale Dienste vollständig verwalten (Super-Admin)",
            AppPermissions.DigitalPricingManage => "Preise für digitale Dienste ändern (Super-Admin)",
            AppPermissions.DigitalActivate => "Digitale Dienste für Mandanten aktivieren/deaktivieren (Super-Admin)",
            AppPermissions.DigitalOrdersView => "Online-Bestellungen anzeigen (Website/App; kein POS)",
            AppPermissions.DigitalOrdersManage => "Online-Bestellstatus ändern (Website/App; kein POS/TSE)",
            AppPermissions.DigitalOrdersApprove => "Online-Bestellungen freigeben / POS-Bridge (Super-Admin)",
            AppPermissions.RksvTestHelper => "RKSV Test-Helfer (Demo-Modus) anzeigen und verwenden",
            AppPermissions.RksvTseSimulation => "TSE-Simulation im RKSV Test-Helfer zurücksetzen",
            AppPermissions.RiskView => "Risiko-Scores und Anomalien anzeigen",
            AppPermissions.RiskManage => "Risiko-Scores auflösen und Maßnahmen auslösen",
            _ => null,
        };
    }

    /// <summary>
    /// Returns true if the permission key is in the catalog (valid for assignment).
    /// </summary>
    public static bool IsValidPermissionKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;
        return PermissionCatalog.All.Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}
