/**
 * Permission constants and helpers — string values must stay aligned with backend
 * `backend/Authorization/AppPermissions.cs` (resource.action names).
 *
 * The backend catalog can grow beyond this object; UI should consume `/me` and role-management
 * catalog responses as the runtime truth. This file is the typed subset the admin shell references.
 *
 * UI decisions (route guard, menu, buttons) should use permissions, not roles.
 */

/** Backend-aligned permission keys (PascalCase) — extend per domain as needed. */
export const AppPermissions = {
  CashRegisterView: 'cash_register.view',
  CashRegisterManage: 'cash_register.manage',
  CashRegisterDecommission: 'cash_register.decommission',
  LicenseView: 'license.view',
} as const;

export const PERMISSIONS = {
  USER_VIEW: 'user.view',
  USER_MANAGE: 'user.manage',
  ROLE_VIEW: 'role.view',
  ROLE_MANAGE: 'role.manage',
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
  TABLE_VIEW: 'table.view',
  TABLE_MANAGE: 'table.manage',
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
  LICENSE_VIEW: AppPermissions.LicenseView,
  /** Issued-license lifecycle (extend/revoke/cancel/soft-delete/unregister); align with backend `AppPermissions.LicenseLifecycleSuper`. */
  LICENSE_LIFECYCLE_SUPER: 'license.super',
  AUDIT_VIEW: 'audit.view',
  AUDIT_EXPORT: 'audit.export',
  AUDIT_CLEANUP: 'audit.cleanup',
  REPORT_VIEW: 'report.view',
  REPORT_EXPORT: 'report.export',
  /** Fiscal export “compliance / legal review” profile (separate from diagnostic-only report.export). */
  FISCAL_EXPORT_COMPLIANCE: 'fiscal.export.compliance',
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
  INVENTORY_DELETE: 'inventory.delete',
  CASHREGISTER_VIEW: AppPermissions.CashRegisterView,
  CASHREGISTER_MANAGE: AppPermissions.CashRegisterManage,
  CASHREGISTER_DECOMMISSION: AppPermissions.CashRegisterDecommission,
  LOCALIZATION_VIEW: 'localization.view',
  LOCALIZATION_MANAGE: 'localization.manage',
  RECEIPT_TEMPLATE_VIEW: 'receipttemplate.view',
  RECEIPT_TEMPLATE_MANAGE: 'receipttemplate.manage',
  TSE_SIGN: 'tse.sign',
  TSE_DIAGNOSTICS: 'tse.diagnostics',
  SYSTEM_CRITICAL: 'system.critical',
  TENANT_MANAGE: 'tenant.manage',
  PRICE_OVERRIDE: 'price.override',
  RECEIPT_REPRINT: 'receipt.reprint',
  /** RKSV Jahresbeleg (annual zero receipt); align with backend AppPermissions.RksvJahresbelegCreate. */
  RKSV_JAHRESBELEG_CREATE: 'rksv.jahresbeleg.create',
  RKSV_NULLBELEG_CREATE: 'rksv.nullbeleg.create',
  RKSV_STARTBELEG_CREATE: 'rksv.startbeleg.create',
  RKSV_MONATSBELEG_CREATE: 'rksv.monatsbeleg.create',
  RKSV_SCHLUSSBELEG_CREATE: 'rksv.schlussbeleg.create',
  BENEFIT_VIEW: 'benefit.view',
  BENEFIT_MANAGE: 'benefit.manage',
  VOUCHER_READ: 'voucher.read',
  VOUCHER_CREATE: 'voucher.create',
  VOUCHER_CANCEL: 'voucher.cancel',
  VOUCHER_AUDIT_VIEW: 'voucher.audit.view',
  CUSTOMER_VIEW: 'customer.view',
  CUSTOMER_MANAGE: 'customer.manage',
  SHIFT_VIEW: 'shift.view',
  SHIFT_MANAGE: 'shift.manage',
} as const;

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

/** Menu/route visible to any authenticated user with at least one permission claim. */
export const ANY_AUTHENTICATED_PERMISSION: string[] = [];

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
