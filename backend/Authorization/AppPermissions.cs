namespace KasseAPI_Final.Authorization;

/// <summary>
/// Permission names in resource.action format. Single source of truth for the permission catalog.
/// Used by PermissionCatalog.All and RolePermissionMatrix. Keep in sync with endpoint matrix.
/// </summary>
public static class AppPermissions
{
    // --- User & Role ---
    public const string UserView = "user.view";
    public const string UserManage = "user.manage";
    public const string RoleView = "role.view";
    public const string RoleManage = "role.manage";

    // --- Product, Category, Modifier ---
    public const string ProductView = "product.view";
    public const string ProductManage = "product.manage";
    public const string CategoryView = "category.view";
    public const string CategoryManage = "category.manage";
    public const string ModifierView = "modifier.view";
    public const string ModifierManage = "modifier.manage";

    // --- Order, Table, Cart, Sale ---
    public const string OrderView = "order.view";
    public const string OrderCreate = "order.create";
    public const string OrderUpdate = "order.update";
    public const string OrderCancel = "order.cancel";
    public const string TableView = "table.view";
    public const string TableManage = "table.manage";
    public const string CartView = "cart.view";
    public const string CartManage = "cart.manage";
    public const string SaleView = "sale.view";
    public const string SaleCreate = "sale.create";
    public const string SaleCancel = "sale.cancel";

    // --- Payment, Refund ---
    public const string PaymentView = "payment.view";
    public const string PaymentTake = "payment.take";
    public const string PaymentCancel = "payment.cancel";
    public const string RefundCreate = "refund.create";

    /// <summary>Apply discount to sale (e.g. manual discount, voucher).</summary>
    public const string DiscountApply = "discount.apply";

    // --- CashRegister, Cashdrawer, Shift ---
    public const string CashRegisterView = "cashregister.view";
    public const string CashRegisterManage = "cashregister.manage";
    public const string CashdrawerOpen = "cashdrawer.open";
    public const string CashdrawerClose = "cashdrawer.close";
    public const string ShiftView = "shift.view";
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";

    // --- Inventory, Customer ---
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryAdjust = "inventory.adjust";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";

    // --- Invoice, CreditNote ---
    public const string InvoiceView = "invoice.view";
    public const string InvoiceManage = "invoice.manage";
    public const string InvoiceExport = "invoice.export";
    public const string CreditNoteCreate = "creditnote.create";

    // --- Settings, Localization, ReceiptTemplate ---
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string LocalizationView = "localization.view";
    public const string LocalizationManage = "localization.manage";
    public const string ReceiptTemplateView = "receipttemplate.view";
    public const string ReceiptTemplateManage = "receipttemplate.manage";

    // --- Audit, Report ---
    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";
    public const string AuditCleanup = "audit.cleanup";
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";

    // --- FinanzOnline ---
    public const string FinanzOnlineView = "finanzonline.view";
    public const string FinanzOnlineManage = "finanzonline.manage";
    public const string FinanzOnlineSubmit = "finanzonline.submit";

    // --- Kitchen (order display / status updates) ---
    public const string KitchenView = "kitchen.view";
    public const string KitchenUpdate = "kitchen.update";

    // --- Legacy / convenience (price override, receipt reprint) ---
    public const string PriceOverride = "price.override";
    public const string ReceiptReprint = "receipt.reprint";
}
