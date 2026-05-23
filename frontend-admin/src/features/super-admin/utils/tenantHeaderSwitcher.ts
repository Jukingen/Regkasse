import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { isLocalDevHostname } from '@/features/auth/services/devTenant';
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

export function findTenantById(
    tenants: AdminTenantListItem[],
    tenantId: string,
): AdminTenantListItem | undefined {
    const normalized = tenantId.trim().toLowerCase();
    return tenants.find((row) => row.id.toLowerCase() === normalized);
}

/** Keeps first occurrence per tenant id (API should not duplicate; guards client merges). */
export function dedupeAdminTenantsById<T extends { id: string }>(rows: T[]): T[] {
    const seen = new Map<string, T>();
    for (const row of rows) {
        const key = row.id.trim().toLowerCase();
        if (!seen.has(key)) {
            seen.set(key, row);
        }
    }
    return [...seen.values()];
}

export function isTenantSuspendedOrInactive(
    tenant: Pick<AdminTenantListItem, 'status' | 'isActive'>,
): boolean {
    return tenant.status === 'suspended' || !tenant.isActive;
}

export type ResolveActiveTenantInput = {
    jwtTenantId: string | null;
    jwtTenantSlug: string | null;
    isImpersonating: boolean;
    isDevTenantOverride: boolean;
    devTenantSlug: string | null;
    hostSlug: string;
};

/**
 * Picks the mandant row shown in header badge and dev switcher.
 * Impersonation / dev override first; then JWT tenant_id; then dev slug / host.
 */
export function resolveActiveTenantFromSwitcherList(
    tenants: AdminTenantListItem[],
    input: ResolveActiveTenantInput,
): AdminTenantListItem | null {
    const deduped = dedupeAdminTenantsById(tenants);
    const byId = (id: string) => findTenantById(deduped, id);
    const bySlug = (slug: string) => findTenantBySlug(deduped, slug);

    if (input.isImpersonating && input.jwtTenantId) {
        return byId(input.jwtTenantId) ?? null;
    }
    if (input.isDevTenantOverride && input.devTenantSlug) {
        return bySlug(input.devTenantSlug) ?? null;
    }
    if (input.jwtTenantId) {
        const fromJwt = byId(input.jwtTenantId);
        if (fromJwt) {
            return fromJwt;
        }
    }
    if (input.devTenantSlug) {
        const fromDev = bySlug(input.devTenantSlug);
        if (fromDev) {
            return fromDev;
        }
    }
    if (input.hostSlug && input.hostSlug !== 'admin') {
        return bySlug(input.hostSlug) ?? null;
    }
    if (input.jwtTenantSlug) {
        return bySlug(input.jwtTenantSlug) ?? null;
    }
    return null;
}

/** Dev header switcher: localhost / NODE_ENV=development / *.regkasse.local — never on *.regkasse.at. */
export function shouldShowHeaderDevTenantSwitch(): boolean {
    if (typeof window === 'undefined') {
        return process.env.NODE_ENV === 'development';
    }
    const host = window.location.hostname.toLowerCase();
    if (host === 'regkasse.at' || host.endsWith('.regkasse.at')) {
        return false;
    }
    if (process.env.NODE_ENV === 'development') {
        return true;
    }
    return isLocalDevHostname(host);
}
