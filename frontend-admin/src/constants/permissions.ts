/**
 * Permission constants and primary menu ↔ permission mapping for FA.
 *
 * String values align with backend `AppPermissions.cs` (resource.action format).
 * Full route guard map: `shared/auth/routePermissions.ts` (`ROUTE_PERMISSIONS`).
 * Runtime checks: `shared/auth/menuPermissions.ts` (`isMenuItemAllowed`).
 */
import {
  ANY_AUTHENTICATED_PERMISSION,
  AppPermissions,
  PERMISSIONS,
} from '@/shared/auth/permissions';

export {
  ANY_AUTHENTICATED_PERMISSION,
  AppPermissions,
  hasAllPermissions,
  hasAnyPermission,
  hasPermission,
  type Permission,
  PERMISSIONS,
  type UserWithPermissions,
} from '@/shared/auth/permissions';

/**
 * Primary sidebar routes → required permission(s).
 * - `[]` = any authenticated user with at least one permission claim (e.g. dashboard).
 * - `string` = single permission required.
 * - `string[]` = any one of the listed permissions.
 *
 * Paths use actual App Router hrefs (not legacy aliases).
 * Must stay aligned with `ROUTE_PERMISSIONS` (see `constants/__tests__/permissions.test.ts`).
 */
export const MENU_PERMISSIONS: Record<string, string | string[] | undefined> = {
  // Dashboard — visible to all authenticated tenant users
  '/dashboard': ANY_AUTHENTICATED_PERMISSION,

  // User management
  '/admin/users': PERMISSIONS.USER_VIEW,

  // Access & roles hub
  '/admin/access': PERMISSIONS.USER_VIEW,
  '/admin/access/roles': PERMISSIONS.ROLE_MANAGE,
  '/admin/access/matrix': PERMISSIONS.ROLE_VIEW,
  '/admin/access/permission-history': PERMISSIONS.AUDIT_VIEW,

  // Tenant management (Super Admin)
  '/admin/tenants': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/approvals': PERMISSIONS.SYSTEM_CRITICAL,
  '/tenant': PERMISSIONS.SYSTEM_CRITICAL,
  '/admin/licenses': PERMISSIONS.LICENSE_VIEW,
  '/admin/cash-registers': PERMISSIONS.SYSTEM_CRITICAL,

  // Cash register management
  '/kassenverwaltung': AppPermissions.CashRegisterManage,

  // Product / catalog
  '/products': PERMISSIONS.PRODUCT_VIEW,
  '/categories': PERMISSIONS.CATEGORY_VIEW,
  '/inventory': PERMISSIONS.INVENTORY_VIEW,

  // Reports
  '/reporting': PERMISSIONS.REPORT_VIEW,
  '/admin/reports': PERMISSIONS.REPORT_VIEW,

  // Settings hub & sub-routes
  '/settings': PERMISSIONS.SETTINGS_VIEW,
  '/settings/password': ANY_AUTHENTICATED_PERMISSION,
  '/settings/company': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/working-hours': PERMISSIONS.SETTINGS_VIEW,
  '/settings/website': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_PREVIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.DIGITAL_CREATE,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/settings/digital': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/digital/customer-portal': [
    PERMISSIONS.DIGITAL_VIEW,
    PERMISSIONS.DIGITAL_REQUEST,
    PERMISSIONS.WEBSITE_MANAGE,
  ],
  '/billing/digital': [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  '/admin/digital': [
    PERMISSIONS.DIGITAL_MANAGE,
    PERMISSIONS.DIGITAL_ACTIVATE,
    PERMISSIONS.DIGITAL_PRICING_MANAGE,
    PERMISSIONS.SYSTEM_CRITICAL,
  ],
  '/admin/digital/requests': [PERMISSIONS.DIGITAL_MANAGE, PERMISSIONS.SYSTEM_CRITICAL],
  '/settings/tse': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/finanzonline': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/backup': PERMISSIONS.BACKUP_MANAGE,
  '/settings/session': PERMISSIONS.SETTINGS_VIEW,
  '/settings/sessions': PERMISSIONS.SETTINGS_VIEW,
  '/settings/offline': PERMISSIONS.SETTINGS_MANAGE,
  '/settings/personalization': PERMISSIONS.SETTINGS_VIEW,
  '/settings/appearance': PERMISSIONS.SETTINGS_VIEW,
  '/settings/payment-methods': PERMISSIONS.SETTINGS_VIEW,
  '/settings/backup-dr': PERMISSIONS.SETTINGS_VIEW,
  '/backup': PERMISSIONS.SETTINGS_VIEW,
  '/backup/dashboard': PERMISSIONS.SETTINGS_VIEW,
  '/backup/runs': PERMISSIONS.SETTINGS_VIEW,
  '/backup/configuration': PERMISSIONS.SETTINGS_VIEW,
  '/backup/configuration/schedule': PERMISSIONS.BACKUP_MANAGE,
  '/backup/configuration/platform': PERMISSIONS.SETTINGS_MANAGE,
  '/backup/audit': PERMISSIONS.SETTINGS_VIEW,
  '/backup/config': PERMISSIONS.SETTINGS_VIEW,
  '/backup/logs': PERMISSIONS.SETTINGS_VIEW,
  '/settings/development-mode': PERMISSIONS.SYSTEM_CRITICAL,

  // Backup (legacy redirect)
  '/admin/backup': PERMISSIONS.SETTINGS_VIEW,

  // Audit log
  '/audit-logs': PERMISSIONS.AUDIT_VIEW,
  '/admin/audit/fiscal-exports': PERMISSIONS.AUDIT_VIEW,

  // RKSV / Fiscal hub
  '/rksv': PERMISSIONS.FINANZONLINE_MANAGE,
};
