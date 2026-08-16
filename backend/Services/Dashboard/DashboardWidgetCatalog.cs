using System.Text.Json;
using KasseAPI_Final.Authorization;

namespace KasseAPI_Final.Services.Dashboard;

/// <summary>Static catalog of admin dashboard widgets exposed to FA.</summary>
public static class DashboardWidgetCatalog
{
    public const string ActionRequired = "action-required";
    public const string DailyClosing = "daily-closing";
    public const string ManagerLicenseStatus = "manager-license-status";
    public const string ManagerKpiStrip = "manager-kpi-strip";
    public const string ManagerMonatsbeleg = "manager-monatsbeleg";
    public const string ManagerActivity = "manager-activity";
    public const string ManagerTseHealth = "manager-tse-health";
    public const string ManagerOfflineQueue = "manager-offline-queue";
    public const string ManagerLicenseChecklist = "manager-license-checklist";
    public const string ManagerLicenseSupport = "manager-license-support";
    public const string ManagerHospitalityLinks = "manager-hospitality-links";
    public const string ManagerExportQuickActions = "manager-export-quick-actions";

    public const string TodaySales = "today-sales";
    public const string ActiveCashRegisters = "active-cash-registers";
    public const string LowStockAlerts = "low-stock-alerts";
    public const string RecentUsers = "recent-users";
    public const string RecentPermissionChanges = "recent-permission-changes";
    public const string LicenseExpiry = "license-expiry";
    public const string FinanzOnlineStatus = "finanzonline-status";
    public const string OfflineSystemStatus = "offline-system-status";
    public const string BackupStatus = "backup-status";
    public const string DataRetention = "data-retention";
    public const string TopSellingProducts = "top-selling-products";
    public const string PaymentTrends = "payment-trends";
    public const string SystemMetrics = "system-metrics";

    private static readonly IReadOnlyList<DashboardWidgetDefinition> All =
    [
        // Compliance Handlungsbedarf (Tagesabschluss + RKSV reminders) — default first for Mandanten-Admin.
        new(ActionRequired, "Handlungsbedarf", "Tagesabschluss und RKSV-Sonderbeleg Erinnerungen", AppPermissions.DailyClosingView, 0, true, true),
        new(DailyClosing, "Tagesabschluss", "Heutiger Kassenabschluss, Wochenstand und Schnellaktion", AppPermissions.DailyClosingView, 1, true, true),

        // Manager dashboard cards (migrated from fixed ManagerDashboard layout)
        new(ManagerLicenseStatus, "Lizenz-Status", "Mandantenlizenz Countdown und Gesundheit", AppPermissions.LicenseView, 2, true, true),
        new(ManagerKpiStrip, "Kennzahlen", "Umsatz, Schichten, Personal und Kassenstand", AppPermissions.ReportView, 3, true, true),
        new(ManagerMonatsbeleg, "Monatsbeleg & RKSV", "Monatsbeleg-Status und Compliance-Hinweise", AppPermissions.CashRegisterView, 4, true, true),
        new(ManagerActivity, "Aktivität", "Letzte Aktivitäten im Mandanten", AppPermissions.AuditView, 5, true, true),
        new(ManagerTseHealth, "TSE-Gesundheit", "TSE-Status und Offline-Hinweise", AppPermissions.CashRegisterView, 6, true, true),
        new(ManagerOfflineQueue, "Offline-Warteschlange", "Ausstehende Offline-Zahlungen", AppPermissions.PaymentView, 7, true, true),
        new(ManagerLicenseChecklist, "Lizenz-Checkliste", "Erneuerungs-Checkliste", AppPermissions.LicenseView, 8, true, true),
        new(ManagerLicenseSupport, "Lizenz-Support", "Support- und Zahlungsoptionen", AppPermissions.LicenseView, 9, true, true),
        new(ManagerHospitalityLinks, "Gastronomie-Links", "Schnelle Links für den Betrieb", AppPermissions.CashRegisterView, 10, true, false),
        new(ManagerExportQuickActions, "Export-Schnellaktionen", "Häufige Exporte starten", AppPermissions.ReportExport, 11, true, false),

        // Existing catalog widgets
        new(TodaySales, "Heutiger Umsatz", "Umsatz und Verkäufe für heute", AppPermissions.ReportView, 12, true, true),
        new(PaymentTrends, "Zahlungstrends", "Tägliche, wöchentliche und monatliche Zahlungsentwicklung", AppPermissions.PaymentView, 13, true, true),
        new(ActiveCashRegisters, "Aktive Kassen", "Kassenstatus und Anzahl", AppPermissions.CashRegisterView, 14, true, true),
        new(LowStockAlerts, "Niedriger Bestand", "Artikel unter Mindestbestand", AppPermissions.InventoryView, 15, true, true),
        new(TopSellingProducts, "Top-Artikel", "Meistverkaufte Produkte", AppPermissions.ReportView, 16, true, true),
        new(RecentUsers, "Neue Benutzer", "Zuletzt angelegte Benutzer", AppPermissions.UserView, 17, true, true),
        new(RecentPermissionChanges, "📜 Berechtigungsänderungen", "Zuletzt geänderte Rollen und Berechtigungen", AppPermissions.AuditView, 18, true, true),
        // Slim expiry widget — off by default so Mandanten-Admin uses manager-license-status (no duplicate).
        new(LicenseExpiry, "Lizenzablauf", "Verbleibende Tage Mandantenlizenz", AppPermissions.LicenseManage, 19, false, true),
        new(FinanzOnlineStatus, "FinanzOnline", "Ausstehende oder fehlgeschlagene Übermittlungen", AppPermissions.FinanzOnlineView, 20, true, true),
        new(OfflineSystemStatus, "Offline-System", "Offline-Bestellungen, Sync-Gesundheit und Rückstau", AppPermissions.PaymentView, 21, true, true),
        new(BackupStatus, "Backup-Status", "Letztes Backup, Erfolgsrate und Konfigurationszustand", AppPermissions.SettingsView, 22, true, true),
        new(DataRetention, "Datenaufbewahrung", "Lizenz-Lebenszyklus, Grace/Lock und Löschanfragen (RKSV 7 Jahre)", AppPermissions.BackupManage, 23, true, true),
        new(SystemMetrics, "System-Metriken", "API-Leistung, Cache, Uptime und aktive Mandanten", AppPermissions.SystemCritical, 24, true, true),
    ];

    public static IReadOnlyList<DashboardWidgetDefinition> GetAll() => All;

    public static DashboardWidgetDefinition? Find(string widgetId) =>
        All.FirstOrDefault(w => string.Equals(w.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<DashboardWidgetDefinition> FilterByPermissions(IEnumerable<string> grantedPermissions)
    {
        var set = new HashSet<string>(grantedPermissions, StringComparer.OrdinalIgnoreCase);
        // Exact Contains would hide cash_register.view widgets from Mandanten-Admin
        // (matrix grants cash_register.manage only; manage→view via PermissionImplication).
        return All.Where(w => PermissionImplication.IsSatisfied(w.RequiredPermission, set)).ToList();
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
                Settings = w.WidgetId switch
                {
                    TopSellingProducts => new Dictionary<string, JsonElement>
                    {
                        ["period"] = JsonSerializer.SerializeToElement("today"),
                    },
                    PaymentTrends => new Dictionary<string, JsonElement>
                    {
                        ["period"] = JsonSerializer.SerializeToElement("Daily"),
                    },
                    _ => null,
                },
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
