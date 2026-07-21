/** Roles treated as platform operators (not tenant business staff). */
export const PLATFORM_USER_ROLES = ['SuperAdmin'] as const;

export function isPlatformUserRole(role: string | undefined | null): boolean {
  if (!role) return false;
  return (PLATFORM_USER_ROLES as readonly string[]).includes(role);
}

/** Business tenants only — excludes platform host slug `admin`. */
export function isBusinessTenantSlug(slug: string | undefined | null): boolean {
  return Boolean(slug && slug !== 'admin');
}
