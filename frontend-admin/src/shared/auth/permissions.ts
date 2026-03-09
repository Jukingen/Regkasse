/**
 * Permission constants and helpers – aligned with backend AppPermissions.
 * UI decisions (route guard, menu, buttons) should use permissions, not roles.
 */

export const PERMISSIONS = {
  USER_VIEW: 'user.view',
  USER_MANAGE: 'user.manage',
  PRODUCT_VIEW: 'product.view',
  PRODUCT_MANAGE: 'product.manage',
  CATEGORY_VIEW: 'category.view',
  CATEGORY_MANAGE: 'category.manage',
  MODIFIER_VIEW: 'modifier.view',
  MODIFIER_MANAGE: 'modifier.manage',
  ORDER_VIEW: 'order.view',
  ORDER_CREATE: 'order.create',
  ORDER_UPDATE: 'order.update',
  ORDER_CANCEL: 'order.cancel',
  CART_VIEW: 'cart.view',
  CART_MANAGE: 'cart.manage',
  PAYMENT_VIEW: 'payment.view',
  PAYMENT_TAKE: 'payment.take',
  PAYMENT_CANCEL: 'payment.cancel',
  REFUND_CREATE: 'refund.create',
  SALE_VIEW: 'sale.view',
  SALE_CREATE: 'sale.create',
  SETTINGS_VIEW: 'settings.view',
  SETTINGS_MANAGE: 'settings.manage',
  AUDIT_VIEW: 'audit.view',
  AUDIT_EXPORT: 'audit.export',
  AUDIT_CLEANUP: 'audit.cleanup',
  REPORT_VIEW: 'report.view',
  REPORT_EXPORT: 'report.export',
  INVOICE_VIEW: 'invoice.view',
  INVOICE_MANAGE: 'invoice.manage',
  INVOICE_EXPORT: 'invoice.export',
  CREDIT_NOTE_CREATE: 'creditnote.create',
  FINANZONLINE_VIEW: 'finanzonline.view',
  FINANZONLINE_MANAGE: 'finanzonline.manage',
  FINANZONLINE_SUBMIT: 'finanzonline.submit',
  INVENTORY_VIEW: 'inventory.view',
  INVENTORY_MANAGE: 'inventory.manage',
  INVENTORY_ADJUST: 'inventory.adjust',
  RECEIPT_TEMPLATE_VIEW: 'receipttemplate.view',
  RECEIPT_TEMPLATE_MANAGE: 'receipttemplate.manage',
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

export interface UserWithPermissions {
  permissions?: string[];
}

/** Single permission check. */
export function hasPermission(
  user: UserWithPermissions | null | undefined,
  permission: string
): boolean {
  if (!user?.permissions?.length) return false;
  return user.permissions.includes(permission);
}

/** True if user has at least one of the given permissions. */
export function hasAnyPermission(
  user: UserWithPermissions | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.some((p) => user!.permissions!.includes(p));
}

/** True if user has all of the given permissions. */
export function hasAllPermissions(
  user: UserWithPermissions | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.every((p) => user!.permissions!.includes(p));
}
