/**
 * Menu item key → required permission(s). Used to filter sidebar by permission.
 * Empty = show when authenticated. One permission = require it; array = require any.
 */
import { PERMISSIONS } from './permissions';

export const MENU_PERMISSION: Record<string, string | string[] | undefined> = {
  '/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/customers': PERMISSIONS.ORDER_VIEW,
  '/receipts': PERMISSIONS.SALE_VIEW,
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/receipt-generate': PERMISSIONS.SALE_VIEW,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/users': PERMISSIONS.USER_VIEW,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
};

export function isMenuItemAllowed(
  key: string,
  permissions: string[] | undefined
): boolean {
  const required = MENU_PERMISSION[key];
  if (required === undefined) return true;
  if (!permissions?.length) return false;
  const arr = Array.isArray(required) ? required : [required];
  return arr.some((p) => permissions.includes(p));
}
