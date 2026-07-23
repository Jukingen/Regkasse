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
    public const string UserCreate = "user.create";
    public const string UserEdit = "user.edit";
    public const string UserDelete = "user.delete";
    public const string UserChangeRole = "user.change.role";
    public const string UserChangeUsername = "user.change.username";
    public const string UserResetPassword = "user.reset.password";
    public const string RoleView = "role.view";
    public const string RoleManage = "role.manage";

    // --- Product, Category, Modifier ---
    public const string ProductView = "product.view";
    public const string ProductManage = "product.manage";
    public const string ProductCreate = "product.create";
    public const string ProductEdit = "product.edit";
    public const string ProductDelete = "product.delete";
    public const string ProductUpdateStock = "product.update.stock";
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

    /// <summary>Reserved — no live endpoint gate. Prefer report.export for payment CSV/PDF exports.</summary>
    [Obsolete("Unused catalog key; no controller gate. Planned removal after 2026-12-31.")]
    public const string PaymentExport = "payment.export";

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
    public const string CashRegisterView = "cash_register.view";
    public const string CashRegisterManage = "cash_register.manage";
    /// <summary>Permanent cash register decommission (Stilllegen) via admin API; issues RKSV Schlussbeleg internally.</summary>
    public const string CashRegisterDecommission = "cash_register.decommission";
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

    /// <summary>Reserved — credit notes use <see cref="InvoiceManage"/> on the live API.</summary>
    [Obsolete("Unused; CreateCreditNote is gated by invoice.manage. Planned removal after 2026-12-31.")]
    public const string CreditNoteCreate = "creditnote.create";

    // --- Settings, Localization, ReceiptTemplate ---
    public const string SettingsView = "settings.view";
    public const string SettingsManage = "settings.manage";
    public const string SettingsBackup = "settings.backup";

    /// <summary>
    /// Tenant-scoped backup management: enqueue manual backups and edit backup schedule/automation settings.
    /// Narrower than <see cref="SettingsManage"/> (which also gates license, NTP, payment methods, banking, etc.);
    /// granted to Manager for own-tenant backups. Execution-mode, artifact download and restore stay on <see cref="SettingsManage"/>.
    /// </summary>
    public const string BackupManage = "backup.manage";

    /// <summary>
    /// Tenant domain / customization management (FA).
    /// Narrower than <see cref="SettingsManage"/>; granted to Manager for own tenant.
    /// Implies <see cref="DigitalView"/> / <see cref="DigitalPreview"/> / <see cref="DigitalRequest"/> —
    /// not create/publish/delete.
    /// </summary>
    public const string WebsiteManage = "website.manage";

    // --- Digital services (simplified surface + legacy web/app keys) ---
    /// <summary>View own digital services (website + app status, templates, URLs).</summary>
    public const string DigitalView = "digital.view";

    /// <summary>Preview own website/app (non-destructive).</summary>
    public const string DigitalPreview = "digital.preview";

    /// <summary>Request website/app creation (Super Admin approval queue).</summary>
    public const string DigitalRequest = "digital.request";

    /// <summary>Create / generate websites and apps (Super Admin).</summary>
    public const string DigitalCreate = "digital.create";

    /// <summary>Publish / re-publish websites and apps (Super Admin).</summary>
    public const string DigitalPublish = "digital.publish";

    /// <summary>Edit digital service configuration (Super Admin).</summary>
    public const string DigitalEdit = "digital.edit";

    /// <summary>Delete digital service artifacts (Super Admin; catalog reserved).</summary>
    public const string DigitalDelete = "digital.delete";

    /// <summary>Legacy: view website only — prefer <see cref="DigitalView"/>.</summary>
    public const string DigitalWebView = "digital.web.view";

    /// <summary>Legacy: preview website — prefer <see cref="DigitalPreview"/>.</summary>
    public const string DigitalWebPreview = "digital.web.preview";

    /// <summary>Legacy: request website — prefer <see cref="DigitalRequest"/>.</summary>
    public const string DigitalWebRequest = "digital.web.request";

    /// <summary>Legacy: create website — prefer <see cref="DigitalCreate"/>.</summary>
    public const string DigitalWebCreate = "digital.web.create";

    /// <summary>Legacy: publish website — prefer <see cref="DigitalPublish"/>.</summary>
    public const string DigitalWebPublish = "digital.web.publish";

    /// <summary>Legacy: delete website — prefer <see cref="DigitalDelete"/>.</summary>
    public const string DigitalWebDelete = "digital.web.delete";

    /// <summary>Legacy generate key — prefer <see cref="DigitalCreate"/>.</summary>
    public const string DigitalWebUse = "digital.web.use";

    /// <summary>Legacy: view app — prefer <see cref="DigitalView"/>.</summary>
    public const string DigitalAppView = "digital.app.view";

    /// <summary>Legacy: preview app — prefer <see cref="DigitalPreview"/>.</summary>
    public const string DigitalAppPreview = "digital.app.preview";

    /// <summary>Legacy: request app — prefer <see cref="DigitalRequest"/>.</summary>
    public const string DigitalAppRequest = "digital.app.request";

    /// <summary>Legacy: create app — prefer <see cref="DigitalCreate"/>.</summary>
    public const string DigitalAppCreate = "digital.app.create";

    /// <summary>Legacy: publish app — prefer <see cref="DigitalPublish"/>.</summary>
    public const string DigitalAppPublish = "digital.app.publish";

    /// <summary>Legacy: delete app — prefer <see cref="DigitalDelete"/>.</summary>
    public const string DigitalAppDelete = "digital.app.delete";

    /// <summary>Legacy generate key — prefer <see cref="DigitalCreate"/>.</summary>
    public const string DigitalAppUse = "digital.app.use";

    /// <summary>
    /// Full digital-services control (SuperAdmin). Implies simplified + legacy web/app keys,
    /// pricing.manage, and activate.
    /// </summary>
    public const string DigitalManage = "digital.manage";

    /// <summary>Change digital-service list prices (SuperAdmin via catalog).</summary>
    public const string DigitalPricingManage = "digital.pricing.manage";

    /// <summary>Activate / deactivate digital-service subscriptions for tenants (SuperAdmin via catalog).</summary>
    public const string DigitalActivate = "digital.activate";

    /// <summary>View website/app online orders (Manager FA inbox; not POS/fiscal).</summary>
    public const string DigitalOrdersView = "digital.orders.view";

    /// <summary>Update online-order status lifecycle (Manager; not POS push / TSE).</summary>
    public const string DigitalOrdersManage = "digital.orders.manage";

    /// <summary>
    /// Approve/override online orders including optional POS cart bridge (Super Admin).
    /// Not granted to Manager — Manager uses status-only <see cref="DigitalOrdersManage"/>.
    /// </summary>
    public const string DigitalOrdersApprove = "digital.orders.approve";

    public const string LocalizationView = "localization.view";
    public const string LocalizationManage = "localization.manage";
    public const string ReceiptTemplateView = "receipttemplate.view";
    public const string ReceiptTemplateManage = "receipttemplate.manage";

    // --- Audit, Report ---
    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";
    public const string AuditDelete = "audit.delete";
    public const string AuditCleanup = "audit.cleanup";
    public const string ReportView = "report.view";
    public const string ReportExport = "report.export";

    /// <summary>Reserved — operational/audit schedules do not use this permission key.</summary>
    [Obsolete("Unused catalog key; no controller gate. Planned removal after 2026-12-31.")]
    public const string ReportSchedule = "report.schedule";

    // --- Daily closing (Tagesabschluss) ---
    /// <summary>View daily closing (Tagesabschluss) data/reports.</summary>
    public const string DailyClosingView = "daily-closing.view";

    /// <summary>Perform (execute) a daily closing (Tagesabschluss).</summary>
    public const string DailyClosingExecute = "daily-closing.execute";

    /// <summary>Uyum / hukuki inceleme paketi (fiscal export compliance profili). Tanılama veya denetim devrinden ayrı.</summary>
    public const string FiscalExportCompliance = "fiscal.export.compliance";

    // --- FinanzOnline ---
    public const string FinanzOnlineView = "finanzonline.view";
    public const string FinanzOnlineManage = "finanzonline.manage";
    public const string FinanzOnlineSubmit = "finanzonline.submit";

    // --- Kitchen (order display / status updates; GRANT_ONLY until KDS endpoints gate these) ---
    public const string KitchenView = "kitchen.view";
    public const string KitchenUpdate = "kitchen.update";

    // --- TSE (fiscal signing) ---
    public const string TseSign = "tse.sign";

    /// <summary>Reserved for future TSE diagnostics admin API; SuperAdmin-only via catalog today.</summary>
    [Obsolete("No live controller gate yet; keep for SuperAdmin policy tests. Planned removal or wire-up after 2026-12-31.")]
    public const string TseDiagnostics = "tse.diagnostics";

    /// <summary>RKSV Monats-Nullbeleg (zero TSE receipt) — admin API only; not POS <see cref="PaymentTake"/>.</summary>
    public const string RksvNullbelegCreate = "rksv.nullbeleg.create";

    /// <summary>RKSV Startbeleg (first zero TSE receipt per register). POS may call when session gate requires it.</summary>
    public const string RksvStartbelegCreate = "rksv.startbeleg.create";

    public const string RksvMonatsbelegCreate = "rksv.monatsbeleg.create";

    /// <summary>View RKSV Monatsbeleg snapshot (monthly closing aggregation).</summary>
    public const string RksvMonatsbelegView = "rksv.monatsbeleg.view";

    /// <summary>RKSV Jahresbeleg (annual zero TSE receipt per register/year). Manual or December monthly path.</summary>
    public const string RksvJahresbelegCreate = "rksv.jahresbeleg.create";

    /// <summary>View RKSV Jahresbeleg snapshot (yearly closing aggregation).</summary>
    public const string RksvJahresbelegView = "rksv.jahresbeleg.view";

    /// <summary>RKSV Schlussbeleg / Endbeleg — permanent cash register decommissioning (manager-level).</summary>
    public const string RksvSchlussbelegCreate = "rksv.schlussbeleg.create";

    /// <summary>RKSV demo/test helper tools on the Sonderbelege page. SuperAdmin-only via catalog; never granted to Manager.</summary>
    public const string RksvTestHelper = "rksv.test-helper";

    /// <summary>Reset the TSE simulation state from the RKSV demo helper. SuperAdmin-only via catalog; never granted to Manager.</summary>
    public const string RksvTseSimulation = "rksv.tse-simulation";

    // --- Risk scoring / anomaly detection ---
    /// <summary>View risk scores and anomaly dashboard (Super Admin cross-tenant inbox uses SystemCritical).</summary>
    public const string RiskView = "risk.view";

    /// <summary>Resolve risk scores and trigger remediations from the risk dashboard.</summary>
    public const string RiskManage = "risk.manage";

    // --- System-critical (permanent delete, high-risk) ---
    public const string SystemCritical = "system.critical";

    /// <summary>Super-admin tenant CRUD and impersonation (<c>/api/admin/tenants</c>).</summary>
    public const string TenantManage = "tenant.manage";
    public const string TenantView = "tenant.view";
    public const string TenantCreate = "tenant.create";
    public const string TenantEdit = "tenant.edit";
    public const string TenantDelete = "tenant.delete";
    public const string TenantImpersonate = "tenant.impersonate";

    /// <summary>Destructive / sensitive issued-license lifecycle (extend in-place, cancel, soft-delete, unregister). SuperAdmin-only via catalog.</summary>
    public const string LicenseLifecycleSuper = "license.super";

    /// <summary>Read-only tenant license inventory across the SaaS platform (SuperAdmin-only by default matrix).</summary>
    public const string LicenseView = "license.view";

    /// <summary>View and update the effective tenant (Mandant) license row — own tenant only for Manager.</summary>
    public const string LicenseManage = "license.manage";

    // --- Legacy / convenience (price override, receipt reprint) ---
    public const string PriceOverride = "price.override";
    public const string ReceiptReprint = "receipt.reprint";
}
