namespace KasseAPI_Final.Authorization;

/// <summary>
/// Permission names in resource.action format. Must match frontend and any DB seed.
/// </summary>
public static class Permissions
{
    // Users
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";

    // Products & Categories
    public const string ProductView = "product.view";
    public const string ProductManage = "product.manage";
    public const string CategoryView = "category.view";
    public const string CategoryManage = "category.manage";

    // Reports & Audit
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";
    public const string AuditView = "audit.view";

    // Sales & Payment
    public const string SaleCreate = "sale.create";
    public const string SaleView = "sale.view";
    public const string PaymentTake = "payment.take";
    public const string RefundCreate = "refund.create";
    public const string PriceOverride = "price.override";

    // Table & Orders
    public const string OrderCreate = "order.create";
    public const string OrderUpdate = "order.update";
    public const string OrderView = "order.view";
    public const string TableView = "table.view";
    public const string TableManage = "table.manage";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";

    // Shift & Cash
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    public const string CashdrawerOpen = "cashdrawer.open";

    // Settings & System
    public const string SettingsManage = "settings.manage";
    public const string ReceiptReprint = "receipt.reprint";
    public const string ReceiptVoid = "receipt.void";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";

    /// <summary>
    /// All permission names for policy registration and validation.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        UserView, UserManage,
        ProductView, ProductManage, CategoryView, CategoryManage,
        ReportView, ReportExport, AuditView,
        SaleCreate, SaleView, PaymentTake, RefundCreate, PriceOverride,
        OrderCreate, OrderUpdate, OrderView, TableView, TableManage, CustomerView, CustomerManage,
        ShiftOpen, ShiftClose, CashdrawerOpen,
        SettingsManage, ReceiptReprint, ReceiptVoid, InventoryView, InventoryManage
    };

    /// <summary>
    /// Policy name prefix for permission-based policies.
    /// </summary>
    public const string PolicyPrefix = "Permission:";
}
