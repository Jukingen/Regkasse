import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { getMandantLicenseBadgeDisplay } from '@/features/tenant/utils/mandantLicenseBadge';

export type TenantHeaderIndicatorKind = 'activeWithAdmin' | 'activeNoAdmin' | 'suspended' | 'deleted';

export type TenantHeaderIndicator = {
    kind: TenantHeaderIndicatorKind;
    emoji: string;
};

export type TenantHeaderDetailLines = {
    adminLine: string | null;
    licenseLine: string | null;
};

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

/** Status emoji for header switcher rows (🟢 / 🟡 / 🔴). */
export function getTenantStatusIcon(
    tenant: Pick<AdminTenantListItem, 'status' | 'isActive' | 'ownerAdminEmail'>,
): string {
    return getTenantHeaderIndicator(tenant).emoji;
}

/** Active tenant without owner admin — show warning pill in switcher. */
export function tenantHeaderShowsNoAdminWarning(
    tenant: Pick<AdminTenantListItem, 'status' | 'isActive' | 'ownerAdminEmail'>,
): boolean {
    return getTenantHeaderIndicator(tenant).kind === 'activeNoAdmin';
}

export function getTenantHeaderIndicator(
    tenant: Pick<AdminTenantListItem, 'status' | 'isActive' | 'ownerAdminEmail'>,
): TenantHeaderIndicator {
    if (tenant.status === 'deleted') {
        return { kind: 'deleted', emoji: '⚫' };
    }
    if (tenant.status === 'suspended' || !tenant.isActive) {
        return { kind: 'suspended', emoji: '🔴' };
    }
    if (tenant.ownerAdminEmail?.trim()) {
        return { kind: 'activeWithAdmin', emoji: '🟢' };
    }
    return { kind: 'activeNoAdmin', emoji: '🟡' };
}

/** Primary line: indicator + name (slug) + optional suspended suffix. */
export function getTenantHeaderTitle(
    tenant: Pick<AdminTenantListItem, 'name' | 'slug' | 'status' | 'isActive' | 'ownerAdminEmail'>,
    t: TranslateFn,
): string {
    const { emoji } = getTenantHeaderIndicator(tenant);
    const base = `${tenant.name} (${tenant.slug})`;
    if (tenant.status === 'suspended' || !tenant.isActive) {
        return `${emoji} ${base} - ${t('adminShell.tenant.devSwitcher.suspendedSuffix')}`;
    }
    return `${emoji} ${base}`;
}

export function getTenantHeaderDetailLines(
    tenant: AdminTenantListItem,
    t: TranslateFn,
): TenantHeaderDetailLines {
    const indicator = getTenantHeaderIndicator(tenant);
    let adminLine: string | null = null;

    if (indicator.kind !== 'suspended') {
        const email = tenant.ownerAdminEmail?.trim();
        if (email) {
            adminLine = t('adminShell.tenant.devSwitcher.adminLine', { email });
        }
    }

    const licenseLine = formatLicenseLine(tenant, t);
    return { adminLine, licenseLine };
}

function formatLicenseLine(tenant: AdminTenantListItem, t: TranslateFn): string | null {
    const badge = getTenantSwitcherLicenseBadge(tenant, t);
    return badge?.label ?? null;
}

/** Mandant SaaS license line for header switcher rows (not deployment/on-premise license). */
export function getTenantSwitcherLicenseBadge(
    tenant: Pick<AdminTenantListItem, 'licenseValidUntilUtc' | 'licenseKey'>,
    t: TranslateFn,
): { label: string; color: string; tooltip: string; daysRemaining: number | null } | null {
    const display = getMandantLicenseBadgeDisplay(
        tenant.licenseValidUntilUtc,
        tenant.licenseKey,
        t,
    );
    if (!display) {
        return null;
    }
    return {
        label: display.label,
        color: display.color,
        tooltip: display.tooltip,
        daysRemaining: display.daysRemaining,
    };
}

export function sortTenantsForHeaderSwitcher(tenants: AdminTenantListItem[]): AdminTenantListItem[] {
    return [...tenants].sort((a, b) => {
        const aSuspended = a.status === 'suspended' || !a.isActive;
        const bSuspended = b.status === 'suspended' || !b.isActive;
        if (aSuspended !== bSuspended) {
            return aSuspended ? 1 : -1;
        }
        return a.name.localeCompare(b.name, 'de');
    });
}

export function filterTenantsForHeaderSearch(
    tenants: AdminTenantListItem[],
    query: string,
): AdminTenantListItem[] {
    const q = query.trim().toLowerCase();
    if (!q) {
        return tenants;
    }
    return tenants.filter((row) => {
        const haystack = [row.name, row.slug, row.ownerAdminEmail ?? '', row.email ?? '']
            .join(' ')
            .toLowerCase();
        return haystack.includes(q);
    });
}

export function findTenantBySlug(
    tenants: AdminTenantListItem[],
    slug: string,
): AdminTenantListItem | undefined {
    const normalized = slug.trim().toLowerCase();
    return tenants.find((row) => row.slug.toLowerCase() === normalized);
}
