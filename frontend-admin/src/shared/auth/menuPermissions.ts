/**
 * Menu item key → required permission(s). Used to filter sidebar by permission.
 * Empty = show when authenticated. One permission = require it; array = require any.
 */
import { PERMISSIONS } from './permissions';

export const MENU_PERMISSION: Record<string, string | string[] | undefined> = {
  '/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/invoices': PERMISSIONS.INVOICE_VIEW,
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/pricing-rules': PERMISSIONS.PRODUCT_VIEW,
  '/modifier-groups': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/inventory': PERMISSIONS.INVENTORY_VIEW,
  '/customers': PERMISSIONS.CUSTOMER_VIEW,
  '/receipts': PERMISSIONS.SALE_VIEW,
  '/operations-center': [
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.TSE_SIGN,
    PERMISSIONS.RECEIPT_REPRINT,
    PERMISSIONS.REPORT_EXPORT,
  ],
  '/receipt-templates': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/receipt-generate': PERMISSIONS.RECEIPT_TEMPLATE_VIEW,
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/payments': PERMISSIONS.PAYMENT_VIEW,
  '/reporting': PERMISSIONS.REPORT_VIEW,
  '/reporting/report-center': PERMISSIONS.REPORT_VIEW,
  '/reporting/staff': PERMISSIONS.REPORT_VIEW,
  '/reporting/tagesbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/monatsbericht': PERMISSIONS.REPORT_VIEW,
  '/reporting/jahresbericht': PERMISSIONS.REPORT_VIEW,
  '/tables': PERMISSIONS.TABLE_VIEW,
  '/tagesabschluss': PERMISSIONS.TSE_SIGN,
  '/users': PERMISSIONS.USER_VIEW,
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/settings/payment-methods': PERMISSIONS.SETTINGS_VIEW,
  '/settings/backup-dr': PERMISSIONS.SETTINGS_VIEW,
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/status': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/cmc-certificate': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/verifications': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/finanz-online-operations': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-outbox': PERMISSIONS.FINANZONLINE_VIEW,
  '/rksv/finanz-online-queue': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/fiscal-export-diagnostics': PERMISSIONS.REPORT_EXPORT,
  '/rksv/replay-batch': PERMISSIONS.SETTINGS_VIEW,
  '/rksv/operations': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/incident': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/payload-hash-conflicts': PERMISSIONS.FINANZONLINE_MANAGE,
  '/rksv/offline-intent-coverage': PERMISSIONS.REPORT_EXPORT,
  '/rksv/integrity': [PERMISSIONS.AUDIT_VIEW, PERMISSIONS.FINANZONLINE_MANAGE],
  '/benefit-definitions': PERMISSIONS.BENEFIT_VIEW,
  '/benefit-assignments': PERMISSIONS.BENEFIT_VIEW,
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
