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
    /// <summary>
    /// Broader cash-register awareness for POS (e.g. waiter): relaxes <em>settings assignment</em> validation so an open register
    /// on another user&apos;s shift may still be stored as the user&apos;s persisted cash-register assignment (preference / routing).
    /// Does <strong>not</strong> relax operational shift ownership: payment validation, POS picker listing, and ensure-ready conflict
    /// checks still treat another user&apos;s open shift as blocking for payment and &quot;ready&quot; session state.
    /// </summary>
    public const string CashRegisterView = "cashregister.view";
    public const string CashRegisterManage = "cashregister.manage";
    public const string CashdrawerOpen = "cashdrawer.open";
    public const string CashdrawerClose = "cashdrawer.close";
    public const string ShiftView = "shift.view";
    public const string ShiftOpen = "shift.open";
    public const string ShiftClose = "shift.close";
    /// <summary>Combined shift management (open/close/config). Use when action spans both open and close.</summary>
    public const string ShiftManage = "shift.manage";

    // --- Customer benefits (definitions and assignments) ---
    public const string BenefitView = "benefit.view";
    public const string BenefitManage = "benefit.manage";

    // --- Vouchers (Gutscheine, admin back-office) ---
    public const string VoucherRead = "voucher.read";
    public const string VoucherCreate = "voucher.create";
    public const string VoucherCancel = "voucher.cancel";
    public const string VoucherAuditView = "voucher.audit.view";
    /// <summary>POS/back-office issuance of stored-value vouchers (RKSV-non-fiscal; no TSE).</summary>
    public const string VoucherIssue = "voucher.issue";

    // --- Inventory, Customer ---
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryDelete = "inventory.delete";
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

    /// <summary>Uyum / hukuki inceleme paketi (fiscal export compliance profili). Tanılama veya denetim devrinden ayrı.</summary>
    public const string FiscalExportCompliance = "fiscal.export.compliance";

    // --- FinanzOnline ---
    public const string FinanzOnlineView = "finanzonline.view";
    public const string FinanzOnlineManage = "finanzonline.manage";
    public const string FinanzOnlineSubmit = "finanzonline.submit";

    // --- Kitchen (order display / status updates) ---
    public const string KitchenView = "kitchen.view";
    public const string KitchenUpdate = "kitchen.update";

    // --- TSE (fiscal signing) ---
    public const string TseSign = "tse.sign";
    public const string TseDiagnostics = "tse.diagnostics";

    /// <summary>RKSV Monats-Nullbeleg (zero TSE receipt) — admin API only; not POS <see cref="PaymentTake"/>.</summary>
    public const string RksvNullbelegCreate = "rksv.nullbeleg.create";

    /// <summary>RKSV Startbeleg (first zero TSE receipt per register). POS may call when session gate requires it.</summary>
    public const string RksvStartbelegCreate = "rksv.startbeleg.create";

    public const string RksvMonatsbelegCreate = "rksv.monatsbeleg.create";

    /// <summary>RKSV Jahresbeleg (annual zero TSE receipt per register/year). Manual or December monthly path.</summary>
    public const string RksvJahresbelegCreate = "rksv.jahresbeleg.create";

    /// <summary>RKSV Schlussbeleg / Endbeleg — permanent cash register decommissioning (manager-level).</summary>
    public const string RksvSchlussbelegCreate = "rksv.schlussbeleg.create";

    // --- System-critical (permanent delete, high-risk) ---
    public const string SystemCritical = "system.critical";

    /// <summary>Destructive / sensitive issued-license lifecycle (extend in-place, cancel, soft-delete, unregister). SuperAdmin-only via catalog.</summary>
    public const string LicenseLifecycleSuper = "license.super";

    // --- Legacy / convenience (price override, receipt reprint) ---
    public const string PriceOverride = "price.override";
    public const string ReceiptReprint = "receipt.reprint";
}
