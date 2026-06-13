/**
 * Admin JWT permission fixtures — must stay aligned with
 * `backend/Authorization/AdminAppPermissionProfile.cs` + `RolePermissionMatrix.cs`.
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

/** Representative Manager admin permissions (matrix minus POS-terminal-only strip). */
export const MANAGER_ADMIN_PERMISSIONS: readonly string[] = [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.ROLE_VIEW,
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.PRODUCT_MANAGE,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.CASHREGISTER_MANAGE,
    PERMISSIONS.SETTINGS_VIEW,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.FINANZONLINE_MANAGE,
    PERMISSIONS.FINANZONLINE_VIEW,
    PERMISSIONS.TABLE_VIEW,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.SHIFT_VIEW,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.INVENTORY_VIEW,
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.VOUCHER_READ,
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

/** Menus Manager must see with admin-filtered permissions. */
export const MANAGER_REQUIRED_MENU_KEYS: readonly string[] = [
    '/dashboard',
    '/kassenverwaltung',
    '/admin/users',
    '/admin/access',
    '/admin/access/roles',
    '/settings/company',
    '/rksv/operations',
    '/audit-logs',
];
