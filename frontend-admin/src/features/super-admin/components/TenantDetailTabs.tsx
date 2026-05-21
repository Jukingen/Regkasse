'use client';

/**
 * Tab key helpers for the super-admin tenant detail dashboard.
 * Page composition lives in `app/(protected)/admin/tenants/[tenantId]/page.tsx`.
 */
export const TENANT_DETAIL_TAB_KEYS = ['overview', 'users', 'registers', 'license', 'settings'] as const;

export type TenantDetailTabKey = (typeof TENANT_DETAIL_TAB_KEYS)[number];

export function parseTenantDetailTab(raw: string | null): TenantDetailTabKey {
    if (raw && (TENANT_DETAIL_TAB_KEYS as readonly string[]).includes(raw)) {
        return raw as TenantDetailTabKey;
    }
    return 'overview';
}
