import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';

export type TenantSelectorStatusKind = 'demo' | 'admin' | 'noAdmin';

export type TenantSelectorStatus = {
    kind: TenantSelectorStatusKind;
    suffix: string;
};

/**
 * German operator-facing status suffix for tenant dropdown/table (emojis per product spec).
 */
export function getTenantSelectorStatus(
    tenant: Pick<AdminTenantListItem, 'ownerAdminEmail' | 'isDemoPreset'>,
    t: (key: string, params?: Record<string, string>) => string,
): TenantSelectorStatus {
    if (tenant.isDemoPreset) {
        return {
            kind: 'demo',
            suffix: `⚠️ ${t('superadmin.selector.demoTenant')}`,
        };
    }

    const email = tenant.ownerAdminEmail?.trim();
    if (email) {
        return {
            kind: 'admin',
            suffix: `✅ ${t('superadmin.selector.adminAssigned', { email })}`,
        };
    }

    return {
        kind: 'noAdmin',
        suffix: `🔴 ${t('superadmin.selector.noAdmin')}`,
    };
}

/** e.g. "Test Bar (bar) - ✅ Admin: admin@prod.regkasse.at" */
export function buildTenantSelectorLabel(
    tenant: Pick<AdminTenantListItem, 'name' | 'slug' | 'ownerAdminEmail' | 'isDemoPreset'>,
    t: (key: string, params?: Record<string, string>) => string,
): string {
    const status = getTenantSelectorStatus(tenant, t);
    return `${tenant.name} (${tenant.slug}) - ${status.suffix}`;
}
