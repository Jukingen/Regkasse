'use client';

/**
 * Tab key helpers for the super-admin tenant detail dashboard.
 * Page composition lives in `app/(protected)/admin/tenants/[tenantId]/page.tsx`.
 */
export const TENANT_DETAIL_TAB_KEYS = ['overview', 'registers', 'license', 'settings'] as const;

/** Legacy `?tab=users` URLs redirect to `/admin/users?tenantId=`. */
export const TENANT_DETAIL_LEGACY_USERS_TAB = 'users';

export type TenantDetailTabKey = (typeof TENANT_DETAIL_TAB_KEYS)[number];

export function parseTenantDetailTab(
  raw: string | null
): TenantDetailTabKey | typeof TENANT_DETAIL_LEGACY_USERS_TAB {
  if (raw === TENANT_DETAIL_LEGACY_USERS_TAB) {
    return TENANT_DETAIL_LEGACY_USERS_TAB;
  }
  if (raw && (TENANT_DETAIL_TAB_KEYS as readonly string[]).includes(raw)) {
    return raw as TenantDetailTabKey;
  }
  return 'overview';
}
