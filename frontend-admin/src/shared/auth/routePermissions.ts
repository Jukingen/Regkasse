/**
 * Path → required permission(s) for route access.
 * Used by PermissionRouteGuard. Empty array = route requires at least one permission (no specific permission).
 * Fail-closed: no permissions in token → deny unless migration flag is set.
 */
import { PERMISSIONS } from './permissions';

export const ROUTE_PERMISSIONS: Record<string, string | string[]> = {
  '/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/orders': PERMISSIONS.ORDER_VIEW,
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/users': PERMISSIONS.USER_VIEW,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/receipt-generate': PERMISSIONS.SALE_VIEW,
  '/customers': PERMISSIONS.ORDER_VIEW,
  '/receipts': PERMISSIONS.SALE_VIEW,
  '/rksv': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
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
