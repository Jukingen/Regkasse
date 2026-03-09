namespace KasseAPI_Final.Authorization;

/// <summary>
/// Central list of all permissions for policy registration and validation.
/// Built from AppPermissions; single source for AddAuthorization policy loop.
/// </summary>
public static class PermissionCatalog
{
    /// <summary>
    /// Policy name prefix for permission-based policies (e.g. "Permission:report.view").
    /// </summary>
    public const string PolicyPrefix = "Permission:";

    /// <summary>
    /// Claim type for permission entries in JWT (one claim per permission).
    /// </summary>
    public const string PermissionClaimType = "permission";

    /// <summary>
    /// All permission strings. Used to register policies and by RolePermissionMatrix.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        // User & Role
        AppPermissions.UserView,
        AppPermissions.UserManage,
        AppPermissions.RoleView,
        AppPermissions.RoleManage,
        // Product, Category, Modifier
        AppPermissions.ProductView,
        AppPermissions.ProductManage,
        AppPermissions.CategoryView,
        AppPermissions.CategoryManage,
        AppPermissions.ModifierView,
        AppPermissions.ModifierManage,
        // Order, Table, Cart, Sale
        AppPermissions.OrderView,
        AppPermissions.OrderCreate,
        AppPermissions.OrderUpdate,
        AppPermissions.OrderCancel,
        AppPermissions.TableView,
        AppPermissions.TableManage,
        AppPermissions.CartView,
        AppPermissions.CartManage,
        AppPermissions.SaleView,
        AppPermissions.SaleCreate,
        AppPermissions.SaleCancel,
        // Payment, Refund
        AppPermissions.PaymentView,
        AppPermissions.PaymentTake,
        AppPermissions.PaymentCancel,
        AppPermissions.RefundCreate,
        AppPermissions.DiscountApply,
        // CashRegister, Cashdrawer, Shift
        AppPermissions.CashRegisterView,
        AppPermissions.CashRegisterManage,
        AppPermissions.CashdrawerOpen,
        AppPermissions.CashdrawerClose,
        AppPermissions.ShiftView,
        AppPermissions.ShiftOpen,
        AppPermissions.ShiftClose,
        AppPermissions.ShiftManage,
        // Inventory, Customer
        AppPermissions.InventoryView,
        AppPermissions.InventoryManage,
        AppPermissions.InventoryAdjust,
        AppPermissions.InventoryDelete,
        AppPermissions.CustomerView,
        AppPermissions.CustomerManage,
        // Invoice, CreditNote
        AppPermissions.InvoiceView,
        AppPermissions.InvoiceManage,
        AppPermissions.InvoiceExport,
        AppPermissions.CreditNoteCreate,
        // Settings, Localization, ReceiptTemplate
        AppPermissions.SettingsView,
        AppPermissions.SettingsManage,
        AppPermissions.LocalizationView,
        AppPermissions.LocalizationManage,
        AppPermissions.ReceiptTemplateView,
        AppPermissions.ReceiptTemplateManage,
        // Audit, Report
        AppPermissions.AuditView,
        AppPermissions.AuditExport,
        AppPermissions.AuditCleanup,
        AppPermissions.ReportView,
        AppPermissions.ReportExport,
        // FinanzOnline
        AppPermissions.FinanzOnlineView,
        AppPermissions.FinanzOnlineManage,
        AppPermissions.FinanzOnlineSubmit,
        // Kitchen
        AppPermissions.KitchenView,
        AppPermissions.KitchenUpdate,
        // TSE, system-critical
        AppPermissions.TseSign,
        AppPermissions.TseDiagnostics,
        AppPermissions.SystemCritical,
        // Legacy / convenience
        AppPermissions.PriceOverride,
        AppPermissions.ReceiptReprint,
    };
}
