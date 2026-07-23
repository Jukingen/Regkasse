/**
 * Permission constants and helpers — string values must stay aligned with backend
 * `backend/Authorization/AppPermissions.cs` (resource.action names).
 *
 * Catalog entrypoint (re-export): `./permissionsCatalog.ts`
 * Implication map (menu + UI): `./permissionImplications.ts` (`PERMISSION_IMPLICATIONS`)
 * Consistency gate: `node scripts/verify-permission-keys.mjs` (`npm run verify:permission-keys`)
 *
 * The backend catalog can grow beyond this object; UI should consume `/me` and role-management
 * catalog responses as the runtime truth. This file is the typed subset the admin shell references.
 *
 * UI decisions (route guard, menu, buttons) should use permissions, not roles.
 */

import { permissionImplied } from './permissionImplication';

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
  USER_RESET_PASSWORD: 'user.reset.password',
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
  /** Tenant-scoped backup management (trigger + schedule); narrower than settings.manage. Backend: AppPermissions.BackupManage. */
  BACKUP_MANAGE: 'backup.manage',
  /** Domain / website customization. Backend: AppPermissions.WebsiteManage. */
  WEBSITE_MANAGE: 'website.manage',
  /** View digital services. Backend: AppPermissions.DigitalView. */
  DIGITAL_VIEW: 'digital.view',
  /** Preview digital services. Backend: AppPermissions.DigitalPreview. */
  DIGITAL_PREVIEW: 'digital.preview',
  /** Request digital service creation. Backend: AppPermissions.DigitalRequest. */
  DIGITAL_REQUEST: 'digital.request',
  /** Create websites/apps (Super Admin). Backend: AppPermissions.DigitalCreate. */
  DIGITAL_CREATE: 'digital.create',
  /** Publish websites/apps (Super Admin). Backend: AppPermissions.DigitalPublish. */
  DIGITAL_PUBLISH: 'digital.publish',
  /** Edit digital services (Super Admin). Backend: AppPermissions.DigitalEdit. */
  DIGITAL_EDIT: 'digital.edit',
  /** Delete digital services (Super Admin). Backend: AppPermissions.DigitalDelete. */
  DIGITAL_DELETE: 'digital.delete',
  /** Legacy view website. Backend: AppPermissions.DigitalWebView. */
  DIGITAL_WEB_VIEW: 'digital.web.view',
  /** Legacy preview website. Backend: AppPermissions.DigitalWebPreview. */
  DIGITAL_WEB_PREVIEW: 'digital.web.preview',
  /** Legacy request website. Backend: AppPermissions.DigitalWebRequest. */
  DIGITAL_WEB_REQUEST: 'digital.web.request',
  /** Legacy create website. Backend: AppPermissions.DigitalWebCreate. */
  DIGITAL_WEB_CREATE: 'digital.web.create',
  /** Legacy publish website. Backend: AppPermissions.DigitalWebPublish. */
  DIGITAL_WEB_PUBLISH: 'digital.web.publish',
  /** Legacy delete website. Backend: AppPermissions.DigitalWebDelete. */
  DIGITAL_WEB_DELETE: 'digital.web.delete',
  /** Legacy generate website. Backend: AppPermissions.DigitalWebUse. */
  DIGITAL_WEB_USE: 'digital.web.use',
  /** Legacy view app. Backend: AppPermissions.DigitalAppView. */
  DIGITAL_APP_VIEW: 'digital.app.view',
  /** Legacy preview app. Backend: AppPermissions.DigitalAppPreview. */
  DIGITAL_APP_PREVIEW: 'digital.app.preview',
  /** Legacy request app. Backend: AppPermissions.DigitalAppRequest. */
  DIGITAL_APP_REQUEST: 'digital.app.request',
  /** Legacy create app. Backend: AppPermissions.DigitalAppCreate. */
  DIGITAL_APP_CREATE: 'digital.app.create',
  /** Legacy publish app. Backend: AppPermissions.DigitalAppPublish. */
  DIGITAL_APP_PUBLISH: 'digital.app.publish',
  /** Legacy delete app. Backend: AppPermissions.DigitalAppDelete. */
  DIGITAL_APP_DELETE: 'digital.app.delete',
  /** Legacy generate app / PWA. Backend: AppPermissions.DigitalAppUse. */
  DIGITAL_APP_USE: 'digital.app.use',
  /** Full digital services control (Super Admin). Backend: AppPermissions.DigitalManage. */
  DIGITAL_MANAGE: 'digital.manage',
  /** Change digital service pricing (Super Admin). Backend: AppPermissions.DigitalPricingManage. */
  DIGITAL_PRICING_MANAGE: 'digital.pricing.manage',
  /** Activate/deactivate digital services for tenants (Super Admin). Backend: AppPermissions.DigitalActivate. */
  DIGITAL_ACTIVATE: 'digital.activate',
  /** View website/app online orders. Backend: AppPermissions.DigitalOrdersView. */
  DIGITAL_ORDERS_VIEW: 'digital.orders.view',
  /** Update online-order status (not POS). Backend: AppPermissions.DigitalOrdersManage. */
  DIGITAL_ORDERS_MANAGE: 'digital.orders.manage',
  /** Approve online orders / POS bridge (Super Admin). Backend: AppPermissions.DigitalOrdersApprove. */
  DIGITAL_ORDERS_APPROVE: 'digital.orders.approve',
  LICENSE_VIEW: AppPermissions.LicenseView,
  /** Mandant (tenant) license view/update for own tenant — align with backend `AppPermissions.LicenseManage`. */
  LICENSE_MANAGE: 'license.manage',
  /** Issued-license lifecycle (extend/revoke/cancel/soft-delete/unregister); align with backend `AppPermissions.LicenseLifecycleSuper`. */
  LICENSE_LIFECYCLE_SUPER: 'license.super',
  AUDIT_VIEW: 'audit.view',
  AUDIT_EXPORT: 'audit.export',
  AUDIT_CLEANUP: 'audit.cleanup',
  REPORT_VIEW: 'report.view',
  REPORT_EXPORT: 'report.export',
  /** Daily closing (Tagesabschluss) view/execute; align with backend AppPermissions.DailyClosingView/Execute. */
  DAILY_CLOSING_VIEW: 'daily-closing.view',
  DAILY_CLOSING_EXECUTE: 'daily-closing.execute',
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
  RISK_VIEW: 'risk.view',
  RISK_MANAGE: 'risk.manage',
  TENANT_MANAGE: 'tenant.manage',
  PRICE_OVERRIDE: 'price.override',
  RECEIPT_REPRINT: 'receipt.reprint',
  /** RKSV Jahresbeleg (annual zero receipt); align with backend AppPermissions.RksvJahresbelegCreate. */
  RKSV_JAHRESBELEG_CREATE: 'rksv.jahresbeleg.create',
  RKSV_NULLBELEG_CREATE: 'rksv.nullbeleg.create',
  RKSV_STARTBELEG_CREATE: 'rksv.startbeleg.create',
  RKSV_MONATSBELEG_CREATE: 'rksv.monatsbeleg.create',
  RKSV_SCHLUSSBELEG_CREATE: 'rksv.schlussbeleg.create',
  /** RKSV demo/test helper card (Sonderbelege). SuperAdmin-only via backend catalog; align with AppPermissions.RksvTestHelper. */
  RKSV_TEST_HELPER: 'rksv.test-helper',
  /** Reset TSE simulation from the RKSV demo helper; align with AppPermissions.RksvTseSimulation. */
  RKSV_TSE_SIMULATION: 'rksv.tse-simulation',
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

/** Single permission check (mirrors backend PermissionImplication). */
export function hasPermission(
  user: UserWithPermissions | null | undefined,
  permission: string
): boolean {
  if (!user?.permissions?.length) return false;
  return permissionImplied(permission, user.permissions);
}

/** True if user has at least one of the given permissions. */
export function hasAnyPermission(
  user: UserWithPermissions | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.some((p) => permissionImplied(p, user!.permissions!));
}

/** True if user has all of the given permissions. */
export function hasAllPermissions(
  user: UserWithPermissions | null | undefined,
  permissions: string[]
): boolean {
  if (!user?.permissions?.length || !permissions.length) return false;
  return permissions.every((p) => permissionImplied(p, user!.permissions!));
}
