/**
 * Admin JWT permission fixtures — must stay aligned with
 * `backend/Authorization/AdminAppPermissionProfile.cs` + `RolePermissionMatrix.cs`.
 * Manager oversight reads: `AdminAppPermissionProfile.ManagerOversightViewPermissions`.
 * Manager menu contract: `MANAGER_REQUIRED_MENU_KEYS` / `MANAGER_FORBIDDEN_MENU_KEYS`.
 * Used by role ↔ menu visibility contract tests (CI guardrail).
 */
import { PERMISSIONS } from '@/shared/auth/permissions';

/** Cashier after AdminAppPermissionProfile.Filter(app=admin). */
export const CASHIER_ADMIN_PERMISSIONS: readonly string[] = [
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.MODIFIER_VIEW,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.REPORT_VIEW,
];

/**
 * Representative Manager admin permissions (matrix minus POS-terminal write strip).
 * Keep in sync with backend `RolePermissionMatrix` + `AdminAppPermissionProfile.Filter`.
 */
export const MANAGER_ADMIN_PERMISSIONS: readonly string[] = [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.ROLE_VIEW,
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.PRODUCT_MANAGE,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.MODIFIER_VIEW,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.PAYMENT_CANCEL,
    PERMISSIONS.REFUND_CREATE,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.DAILY_CLOSING_VIEW,
    PERMISSIONS.DAILY_CLOSING_EXECUTE,
    PERMISSIONS.CASHREGISTER_VIEW,
    PERMISSIONS.CASHREGISTER_MANAGE,
    PERMISSIONS.SETTINGS_VIEW,
    PERMISSIONS.BACKUP_MANAGE,
    PERMISSIONS.LICENSE_MANAGE,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.FINANZONLINE_MANAGE,
    PERMISSIONS.FINANZONLINE_VIEW,
    PERMISSIONS.TABLE_VIEW,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.SHIFT_VIEW,
    PERMISSIONS.INVENTORY_VIEW,
    PERMISSIONS.INVENTORY_MANAGE,
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.CUSTOMER_VIEW,
    PERMISSIONS.CART_VIEW,
    PERMISSIONS.VOUCHER_READ,
    PERMISSIONS.FISCAL_EXPORT_COMPLIANCE,
];

/** Menus Cashier must never see in FA (POS-only or admin-only). */
export const CASHIER_FORBIDDEN_MENU_KEYS: readonly string[] = [
    '/tables',
    '/receipts',
    '/shifts',
    '/tagesabschluss',
    '/operations-center',
    '/kassenverwaltung',
    '/users',
    '/admin/users',
    '/settings',
    '/admin/license',
    '/rksv/operations',
    '/audit-logs',
];

/** Menus Cashier must see with admin-filtered permissions. */
export const CASHIER_REQUIRED_MENU_KEYS: readonly string[] = [
    '/dashboard',
    '/products',
    '/payments',
    '/reporting/report-center',
];

/**
 * Manager oversight menus — must remain visible with `MANAGER_ADMIN_PERMISSIONS`.
 * Grouped by sidebar IA; update when adding catalog/RKSV leaves Manager should reach.
 */
export const MANAGER_REQUIRED_MENU_KEYS: readonly string[] = [
    '/dashboard',
    '/operations-center',
    '/tables',
    '/kassenverwaltung',
    '/shifts',
    '/tagesabschluss',
    '/staff',
    '/receipts',
    '/payments',
    '/payments/storno-refund-audit',
    '/payments/trends',
    '/admin/payments/card-transactions',
    '/vouchers',
    '/invoices',
    '/products',
    '/categories',
    '/modifier-groups',
    '/pricing-rules',
    '/inventory',
    '/customers',
    '/reporting',
    '/reporting/report-center',
    '/reporting/staff',
    '/audit-logs/activity',
    '/admin/reports/user-activity',
    '/audit-logs',
    '/audit-logs/activity',
    '/admin/audit/fiscal-exports',
    '/admin/tse/offline-transactions',
    '/rksv/sb/startbeleg',
    '/rksv/sb/monatsbeleg',
    '/rksv/sb/jahresbeleg',
    '/rksv/sb/nullbeleg',
    '/rksv/sb/schlussbeleg',
    '/rksv/operations',
    '/rksv/status',
    '/rksv/finanz-online-outbox',
    '/rksv/offline-orders',
    '/rksv/finanz-online-queue',
    '/rksv/finanz-online-operations',
    '/rksv/incident',
    '/rksv/replay-batch',
    '/rksv/payload-hash-conflicts',
    '/rksv/verifications',
    '/rksv/fiscal-export-diagnostics',
    '/rksv/integrity',
    '/rksv/compliance',
    '/rksv/signature-chain',
    '/rksv/offline-intent-coverage',
    '/rksv/belegcheck',
    '/rksv/cmc-certificate',
    '/admin/rksv/dep-export',
    '/admin/rksv/signature-verify',
    '/admin/users',
    '/admin/access',
    '/admin/access/matrix',
    '/settings',
    '/settings/session',
    '/settings/personalization',
    '/settings/payment-methods',
    '/backup/dashboard',
    '/backup/runs',
    '/admin/license',
];

/**
 * Manager must not see: POS floor signing, platform admin, benefits, receipt templates, dev tooling.
 */
export const MANAGER_FORBIDDEN_MENU_KEYS: readonly string[] = [
    '/admin/access/roles',
    '/receipt-templates',
    '/receipt-generate',
    '/benefit-definitions',
    '/benefit-assignments',
    '/settings/development-mode',
    '/settings/company',
    '/settings/offline',
    '/settings/tse',
    '/settings/finanzonline',
    '/admin/system/time-sync',
    '/admin/tenants',
    '/admin/licenses',
    '/admin/cash-registers',
    '/admin/billing',
    '/rksv/sb/test-helper',
];
