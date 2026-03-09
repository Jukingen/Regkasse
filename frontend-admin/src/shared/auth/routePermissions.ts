/**
 * Path → required permission(s) for route access.
 * Used by PermissionRouteGuard. Empty = authenticated only.
 */
import { PERMISSIONS } from './permissions';

export const ROUTE_PERMISSIONS: Record<string, string | string[]> = {
  '/dashboard': [],
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
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
};
