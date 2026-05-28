/**
 * Path → required permission(s) for route access (longest-prefix match).
 * Used by `PermissionRouteGuard`. Sidebar/menu visibility uses `menuPermissions.MENU_PERMISSION` separately;
 * every sidebar leaf must have matching entries here — enforced by `sidebarRouteCoverage` tests.
 *
 * Empty array = route requires at least one permission (no specific permission).
 * Fail-closed: no permissions in token → deny unless migration flag is set.
 */
import { AppPermissions, PERMISSIONS } from './permissions';

export const ROUTE_PERMISSIONS: Record<string, string | string[]> = {
  '/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/pricing-rules': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/inventory': PERMISSIONS.INVENTORY_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/orders': PERMISSIONS.ORDER_VIEW,
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/payments/storno-refund-audit': PERMISSIONS.PAYMENT_VIEW,
  '/admin/tse/offline-transactions': PERMISSIONS.PAYMENT_VIEW,
  '/vouchers/new': PERMISSIONS.VOUCHER_CREATE,
  '/vouchers': PERMISSIONS.VOUCHER_READ,
  '/reporting': PERMISSIONS.REPORT_VIEW,
  '/reporting/report-center': PERMISSIONS.REPORT_VIEW,
  '/reporting/staff': PERMISSIONS.REPORT_VIEW,
  '/reporting/compliance': PERMISSIONS.REPORT_VIEW,
  '/reporting/tagesbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/monatsbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/jahresbericht': PERMISSIONS.REPORT_VIEW,
  '/reports/daily-closing': PERMISSIONS.REPORT_VIEW,
  '/admin/reports': PERMISSIONS.REPORT_VIEW,
  '/admin/reports/user-activity': PERMISSIONS.REPORT_VIEW,
  '/tables': PERMISSIONS.TABLE_VIEW,
  '/kassenverwaltung': AppPermissions.CashRegisterView,
  '/tagesabschluss': PERMISSIONS.TSE_SIGN,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/admin/audit/fiscal-exports': PERMISSIONS.AUDIT_VIEW,
  '/users': PERMISSIONS.USER_VIEW,
  '/admin/users': PERMISSIONS.USER_VIEW,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/settings/personalization': PERMISSIONS.SETTINGS_VIEW,
  '/settings/payment-methods': PERMISSIONS.SETTINGS_VIEW,
  '/settings/backup-dr': PERMISSIONS.SETTINGS_VIEW,
  '/admin/backup': PERMISSIONS.SETTINGS_VIEW,
  '/settings/development-mode': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/system/time-sync': PERMISSIONS.SETTINGS_MANAGE,
  '/admin/license': PERMISSIONS.SETTINGS_VIEW,
  '/admin/tenants': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/licenses': PERMISSIONS.LICENSE_VIEW,
  '/admin/cash-registers': AppPermissions.CashRegisterView,
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/receipt-generate': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/customers': PERMISSIONS.CUSTOMER_VIEW,
  '/receipts': PERMISSIONS.SALE_VIEW,
  '/operations-center': [
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.TSE_SIGN,
    PERMISSIONS.RECEIPT_REPRINT,
    PERMISSIONS.REPORT_EXPORT,
  ],
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
  /** Explicit hub alias path (same access as `/rksv`; avoids prefix-only drift in audits). */
  '/rksv/operations': PERMISSIONS.FINANZONLINE_MANAGE,
  /** Sidebar-only virtual keys: same access as Sonderbelege page (query focus deep links). */
  '/rksv/sb/startbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sb/schlussbeleg': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/sonderbelege': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-operations': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-outbox': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/fiscal-export-diagnostics': PERMISSIONS.REPORT_EXPORT,
  '/rksv/replay-batch': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/incident': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/payload-hash-conflicts': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/offline-intent-coverage': PERMISSIONS.REPORT_EXPORT,
  '/rksv/integrity': [PERMISSIONS.AUDIT_VIEW, PERMISSIONS.FINANZONLINE_MANAGE],
  '/rksv/compliance': PERMISSIONS.AUDIT_VIEW,
  '/rksv/signature-chain': PERMISSIONS.AUDIT_VIEW,
  '/rksv/belegcheck': PERMISSIONS.PAYMENT_VIEW,
  '/benefit-definitions': PERMISSIONS.BENEFIT_VIEW,
  '/benefit-assignments': PERMISSIONS.BENEFIT_VIEW,
};

/** Sorted route prefixes for longest-match lookup (e.g. /receipt-templates/123 → /receipt-templates). */
const ROUTE_KEYS_SORTED = (Object.keys(ROUTE_PERMISSIONS) as string[]).sort(
  (a, b) => b.length - a.length
);

/**
 * Returns required permission(s) for path. Exact match first, then longest prefix match.
 * Protects dynamic segments (e.g. /receipt-templates/[id], /receipts/[receiptId]).
 */
export function getRequiredPermissionForPath(pathname: string): string | string[] | undefined {
  const normalized = pathname.replace(/\/$/, '') || '/';
  if (ROUTE_PERMISSIONS[normalized] !== undefined) return ROUTE_PERMISSIONS[normalized];
  for (const key of ROUTE_KEYS_SORTED) {
    if (normalized === key || normalized.startsWith(key + '/')) return ROUTE_PERMISSIONS[key];
  }
  return undefined;
}
