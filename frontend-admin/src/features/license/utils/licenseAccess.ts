import { PERMISSIONS, type UserWithPermissions, hasPermission } from '@/shared/auth/permissions';

const MANAGER_ROLE = 'Manager';

export type TenantLicenseAccessContext = {
  hasTenantContext: boolean;
  role?: string | null;
  isSuperAdminPlatformMode?: boolean;
};

/**
 * Manager: `license.manage` + real tenant context.
 * Super Admin: `settings.manage` (deployment) or tenant license via platform.
 */
export function canManageTenantLicense(
  user: UserWithPermissions | null | undefined,
  context: TenantLicenseAccessContext
): boolean {
  if (hasPermission(user, PERMISSIONS.SETTINGS_MANAGE)) {
    return true;
  }

  if (!context.hasTenantContext || context.isSuperAdminPlatformMode) {
    return false;
  }

  const role = context.role?.trim();
  if (role?.toLowerCase() === MANAGER_ROLE.toLowerCase()) {
    return hasPermission(user, PERMISSIONS.LICENSE_MANAGE);
  }

  return hasPermission(user, PERMISSIONS.LICENSE_MANAGE);
}

/** Server-Lizenz (deployment) section — Super Admin / settings.manage only. */
export function canViewDeploymentLicenseSection(
  user: UserWithPermissions | null | undefined
): boolean {
  return hasPermission(user, PERMISSIONS.SETTINGS_MANAGE);
}

/** Route/menu access for `/admin/license`. */
export function canAccessLicenseAdminPage(user: UserWithPermissions | null | undefined): boolean {
  return (
    hasPermission(user, PERMISSIONS.LICENSE_MANAGE) ||
    hasPermission(user, PERMISSIONS.SETTINGS_MANAGE)
  );
}
