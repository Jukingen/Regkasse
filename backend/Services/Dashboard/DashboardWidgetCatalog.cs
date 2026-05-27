using System.Text.Json;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Services.Dashboard;

/// <summary>Static catalog of admin dashboard widgets exposed to FA.</summary>
public static class DashboardWidgetCatalog
{
    public const string TodaySales = "today-sales";
    public const string ActiveCashRegisters = "active-cash-registers";
    public const string LowStockAlerts = "low-stock-alerts";
    public const string RecentUsers = "recent-users";
    public const string LicenseExpiry = "license-expiry";
    public const string FinanzOnlineStatus = "finanzonline-status";
    public const string TopSellingProducts = "top-selling-products";

    private static readonly IReadOnlyList<DashboardWidgetDefinition> All =
    [
        new(TodaySales, "Heutiger Umsatz", "Umsatz und Verkäufe für heute", AppPermissions.ReportView, 0, true, true),
        new(ActiveCashRegisters, "Aktive Kassen", "Kassenstatus und Anzahl", AppPermissions.CashRegisterView, 1, true, true),
        new(LowStockAlerts, "Niedriger Bestand", "Artikel unter Mindestbestand", AppPermissions.InventoryView, 2, true, true),
        new(TopSellingProducts, "Top-Artikel", "Meistverkaufte Produkte", AppPermissions.ReportView, 3, true, true),
        new(RecentUsers, "Neue Benutzer", "Zuletzt angelegte Benutzer", AppPermissions.UserView, 4, true, true),
        new(LicenseExpiry, "Lizenzablauf", "Verbleibende Tage Mandantenlizenz", AppPermissions.SettingsManage, 5, true, true),
        new(FinanzOnlineStatus, "FinanzOnline", "Ausstehende oder fehlgeschlagene Übermittlungen", AppPermissions.FinanzOnlineView, 6, true, true),
    ];

    public static IReadOnlyList<DashboardWidgetDefinition> GetAll() => All;

    public static DashboardWidgetDefinition? Find(string widgetId) =>
        All.FirstOrDefault(w => string.Equals(w.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<DashboardWidgetDefinition> FilterByPermissions(IEnumerable<string> grantedPermissions)
    {
        var set = new HashSet<string>(grantedPermissions, StringComparer.OrdinalIgnoreCase);
        return All.Where(w => set.Contains(w.RequiredPermission)).ToList();
    }

    public static List<Models.DashboardWidget> BuildDefaultLayout(IEnumerable<string> grantedPermissions)
    {
        return FilterByPermissions(grantedPermissions)
            .OrderBy(w => w.DefaultOrder)
            .Select(w => new Models.DashboardWidget
            {
                WidgetId = w.WidgetId,
                Order = w.DefaultOrder,
                IsVisible = w.DefaultVisible,
                Settings = w.WidgetId == TopSellingProducts
                    ? new Dictionary<string, JsonElement>
                    {
                        ["period"] = JsonSerializer.SerializeToElement("today"),
                    }
                    : null,
            })
            .ToList();
    }
}

public sealed record DashboardWidgetDefinition(
    string WidgetId,
    string Title,
    string Description,
    string RequiredPermission,
    int DefaultOrder,
    bool DefaultVisible,
    bool SupportsAutoRefresh);
