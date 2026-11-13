/** Roles treated as platform operators (not tenant business staff). PlatformAdmin is not seeded. */
export const PLATFORM_USER_ROLES = ['SuperAdmin', 'PlatformAdmin'] as const;

/** Rolle filter on /admin/users for a Super Admin (includes platform operators). */
export const ADMIN_USER_ROLE_FILTER_VALUES = [
  'SuperAdmin',
  'Manager',
  'Cashier',
  'Accountant',
  'Waiter',
  'Kitchen',
] as const;

/** Rolle filter for a Mandanten-Admin — own-tenant staff only. */
export const TENANT_STAFF_ROLE_FILTER_VALUES = [
  'Manager',
  'Cashier',
  'Accountant',
  'Waiter',
  'Kitchen',
] as const;

export function roleFilterValuesForActor(isSuperAdminActor: boolean): readonly string[] {
  return isSuperAdminActor ? ADMIN_USER_ROLE_FILTER_VALUES : TENANT_STAFF_ROLE_FILTER_VALUES;
}

const ACTIVITY_STAFF_ROLE_FILTER_VALUES = [
  'Manager',
  'Cashier',
  'Accountant',
  'Waiter',
  'Kitchen',
  'ReportViewer',
  'ComplianceOfficer',
] as const;

/** Rolle filter on /audit-logs/activity. Tenant admins stay at Mandanten-Admin rank and below. */
export function activityRoleFilterValues(isSuperAdminActor: boolean): readonly string[] {
  if (!isSuperAdminActor) return ACTIVITY_STAFF_ROLE_FILTER_VALUES;
  return ['SuperAdmin', 'PlatformAdmin', ...ACTIVITY_STAFF_ROLE_FILTER_VALUES];
}

export function isPlatformUserRole(role: string | undefined | null): boolean {
  if (!role) return false;
  const normalized = role.trim().toLowerCase();
  return PLATFORM_USER_ROLES.some((name) => name.toLowerCase() === normalized);
}

/** Business tenants only — excludes platform host slug `admin` and system sentinel `platform`. */
export function isBusinessTenantSlug(slug: string | undefined | null): boolean {
  if (!slug) return false;
  const normalized = slug.trim().toLowerCase();
  return normalized !== 'admin' && normalized !== 'platform';
}
