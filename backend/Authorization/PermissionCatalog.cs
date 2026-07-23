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
        AppPermissions.UserCreate,
        AppPermissions.UserEdit,
        AppPermissions.UserDelete,
        AppPermissions.UserChangeRole,
        AppPermissions.UserChangeUsername,
        AppPermissions.UserResetPassword,
        AppPermissions.RoleView,
        AppPermissions.RoleManage,
        // Product, Category, Modifier
        AppPermissions.ProductView,
        AppPermissions.ProductManage,
        AppPermissions.ProductCreate,
        AppPermissions.ProductEdit,
        AppPermissions.ProductDelete,
        AppPermissions.ProductUpdateStock,
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
#pragma warning disable CS0618 // Reserved catalog keys until removal window
        AppPermissions.PaymentExport,
#pragma warning restore CS0618
        AppPermissions.RefundCreate,
        AppPermissions.DiscountApply,
        // CashRegister, Cashdrawer, Shift
        AppPermissions.CashRegisterView,
        AppPermissions.CashRegisterManage,
        AppPermissions.CashRegisterDecommission,
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
        AppPermissions.BenefitView,
        AppPermissions.BenefitManage,
        AppPermissions.VoucherRead,
        AppPermissions.VoucherCreate,
        AppPermissions.VoucherCancel,
        AppPermissions.VoucherAuditView,
        AppPermissions.VoucherIssue,
        // Invoice, CreditNote
        AppPermissions.InvoiceView,
        AppPermissions.InvoiceManage,
        AppPermissions.InvoiceExport,
#pragma warning disable CS0618
        AppPermissions.CreditNoteCreate,
#pragma warning restore CS0618
        // Settings, Localization, ReceiptTemplate
        AppPermissions.SettingsView,
        AppPermissions.SettingsManage,
        AppPermissions.SettingsBackup,
        AppPermissions.BackupManage,
        AppPermissions.WebsiteManage,
        AppPermissions.DigitalView,
        AppPermissions.DigitalPreview,
        AppPermissions.DigitalRequest,
        AppPermissions.DigitalCreate,
        AppPermissions.DigitalPublish,
        AppPermissions.DigitalEdit,
        AppPermissions.DigitalDelete,
        AppPermissions.DigitalWebView,
        AppPermissions.DigitalWebPreview,
        AppPermissions.DigitalWebRequest,
        AppPermissions.DigitalWebCreate,
        AppPermissions.DigitalWebPublish,
        AppPermissions.DigitalWebDelete,
        AppPermissions.DigitalWebUse,
        AppPermissions.DigitalAppView,
        AppPermissions.DigitalAppPreview,
        AppPermissions.DigitalAppRequest,
        AppPermissions.DigitalAppCreate,
        AppPermissions.DigitalAppPublish,
        AppPermissions.DigitalAppDelete,
        AppPermissions.DigitalAppUse,
        AppPermissions.DigitalManage,
        AppPermissions.DigitalPricingManage,
        AppPermissions.DigitalActivate,
        AppPermissions.DigitalOrdersView,
        AppPermissions.DigitalOrdersManage,
        AppPermissions.DigitalOrdersApprove,
        AppPermissions.LocalizationView,
        AppPermissions.LocalizationManage,
        AppPermissions.ReceiptTemplateView,
        AppPermissions.ReceiptTemplateManage,
        // Audit, Report
        AppPermissions.AuditView,
        AppPermissions.AuditExport,
        AppPermissions.AuditDelete,
        AppPermissions.AuditCleanup,
        AppPermissions.ReportView,
        AppPermissions.ReportExport,
#pragma warning disable CS0618
        AppPermissions.ReportSchedule,
#pragma warning restore CS0618
        AppPermissions.DailyClosingView,
        AppPermissions.DailyClosingExecute,
        AppPermissions.FiscalExportCompliance,
        // FinanzOnline
        AppPermissions.FinanzOnlineView,
        AppPermissions.FinanzOnlineManage,
        AppPermissions.FinanzOnlineSubmit,
        // Kitchen
        AppPermissions.KitchenView,
        AppPermissions.KitchenUpdate,
        // TSE, system-critical
        AppPermissions.TseSign,
#pragma warning disable CS0618
        AppPermissions.TseDiagnostics,
#pragma warning restore CS0618
        AppPermissions.RksvNullbelegCreate,
        AppPermissions.RksvStartbelegCreate,
        AppPermissions.RksvMonatsbelegCreate,
        AppPermissions.RksvMonatsbelegView,
        AppPermissions.RksvJahresbelegCreate,
        AppPermissions.RksvJahresbelegView,
        AppPermissions.RksvSchlussbelegCreate,
        AppPermissions.RksvTestHelper,
        AppPermissions.RksvTseSimulation,
        AppPermissions.RiskView,
        AppPermissions.RiskManage,
        AppPermissions.SystemCritical,
        AppPermissions.TenantManage,
        AppPermissions.TenantView,
        AppPermissions.TenantCreate,
        AppPermissions.TenantEdit,
        AppPermissions.TenantDelete,
        AppPermissions.TenantImpersonate,
        AppPermissions.LicenseLifecycleSuper,
        AppPermissions.LicenseView,
        AppPermissions.LicenseManage,
        // Legacy / convenience
        AppPermissions.PriceOverride,
        AppPermissions.ReceiptReprint,
    };
}
