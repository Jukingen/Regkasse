/**
 * Permission constants and primary menu ↔ permission mapping for FA.
 *
 * String values align with backend `AppPermissions.cs` (resource.action format).
 * Full route guard map: `shared/auth/routePermissions.ts` (`ROUTE_PERMISSIONS`).
 * Runtime checks: `shared/auth/menuPermissions.ts` (`isMenuItemAllowed`).
 */

export {
  AppPermissions,
  PERMISSIONS,
  ANY_AUTHENTICATED_PERMISSION,
  hasPermission,
  hasAnyPermission,
  hasAllPermissions,
  type Permission,
  type UserWithPermissions,
} from '@/shared/auth/permissions';

import { AppPermissions, PERMISSIONS, ANY_AUTHENTICATED_PERMISSION } from '@/shared/auth/permissions';

/**
 * Primary sidebar routes → required permission(s).
 * - `[]` = any authenticated user with at least one permission claim (e.g. dashboard).
 * - `string` = single permission required.
 * - `string[]` = any one of the listed permissions.
 *
 * Paths use actual App Router hrefs (not legacy aliases).
 */
export const MENU_PERMISSIONS: Record<string, string | string[] | undefined> = {
  // Dashboard — visible to all authenticated tenant users
  '/dashboard': ANY_AUTHENTICATED_PERMISSION,

  // User management
  '/admin/users': PERMISSIONS.USER_VIEW,

  // Access & roles hub
  '/admin/access': PERMISSIONS.USER_VIEW,
  '/admin/access/roles': PERMISSIONS.ROLE_VIEW,
  '/admin/access/matrix': PERMISSIONS.ROLE_VIEW,

  // Tenant management (Super Admin)
  '/admin/tenants': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/licenses': PERMISSIONS.LICENSE_VIEW,
  '/admin/cash-registers': AppPermissions.CashRegisterView,

  // Cash register management
  '/kassenverwaltung': AppPermissions.CashRegisterManage,

  // Product / catalog
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/inventory': PERMISSIONS.INVENTORY_VIEW,

  // Reports
  '/reporting': PERMISSIONS.REPORT_VIEW,
  '/admin/reports': PERMISSIONS.REPORT_VIEW,

  // Settings
  '/settings/company': PERMISSIONS.SETTINGS_VIEW,
  '/settings/session': PERMISSIONS.SETTINGS_VIEW,
  '/settings/personalization': PERMISSIONS.SETTINGS_VIEW,
  '/settings/payment-methods': PERMISSIONS.SETTINGS_VIEW,
  '/settings/backup-dr': PERMISSIONS.SETTINGS_VIEW,
  '/settings/development-mode': PERMISSIONS.SYSTEM_CRITICAL,

  // Backup
  '/admin/backup': PERMISSIONS.SETTINGS_VIEW,

  // Audit log
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/admin/audit/fiscal-exports': PERMISSIONS.AUDIT_VIEW,

  // RKSV / Fiscal hub
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
};
