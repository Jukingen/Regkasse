'use client';

import { useMemo } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { resolveTenantLicenseLabel } from '@/features/super-admin/utils/tenantLicenseLabel';
import {
    filterTenantsForHeaderSearch,
    getTenantStatusIcon,
    sortTenantsForHeaderSwitcher,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';

export type TenantListItemForSwitcher = {
    id: string;
    name: string;
    slug: string;
    status: string;
    isActive: boolean;
    adminEmail: string | null;
    licenseDaysLeft: number | null;
    statusIcon: string;
    /** Raw API row for shared header formatters. */
    source: AdminTenantListItem;
};

function mapTenantForSwitcher(row: AdminTenantListItem): TenantListItemForSwitcher {
    const license = resolveTenantLicenseLabel(
        row.licenseValidUntilUtc,
        row.licenseKey,
        Date.now(),
        row.licenseDaysRemaining,
    );
    return {
        id: row.id,
        name: row.name,
        slug: row.slug,
        status: row.status,
        isActive: row.isActive,
        adminEmail: row.ownerAdminEmail?.trim() ?? null,
        licenseDaysLeft: license.daysRemaining,
        statusIcon: getTenantStatusIcon(row),
        source: row,
    };
}

/**
 * Tenants for the dev header switcher.
 * Super Admin: all non-deleted tenants from the database.
 * Other users: tenants with an active membership (backend-filtered).
 */
export function useTenantListForSwitcher(options?: { includeDeleted?: boolean }) {
    const { user } = useAuth();
    const enabled = process.env.NODE_ENV === 'development' && Boolean(user?.id);
    const includeDeleted = options?.includeDeleted ?? false;

    const query = useGetApiAdminTenants(
        { includeDeleted },
        {
            enabled,
            staleTime: 60_000,
        },
    );

    const tenants = useMemo(
        () => (query.data ?? []).map(mapTenantForSwitcher),
        [query.data],
    );

    return {
        tenants,
        isLoading: query.isLoading,
        isFetching: query.isFetching,
        isError: query.isError,
        error: query.error,
        refetch: query.refetch,
        tenantCount: tenants.length,
    };
}

/** Client-side filter by name, slug, or admin email; preserves switcher sort order. */
export function filterTenantSwitcherItems(
    items: TenantListItemForSwitcher[],
    query: string,
): TenantListItemForSwitcher[] {
    const sortedSources = sortTenantsForHeaderSwitcher(items.map((row) => row.source));
    const filteredSources = filterTenantsForHeaderSearch(sortedSources, query);
    const byId = new Map(items.map((row) => [row.id, row]));
    return filteredSources
        .map((row) => byId.get(row.id))
        .filter((row): row is TenantListItemForSwitcher => row != null);
}

/** Active tenant without an owner admin — Super Admin should confirm before switching. */
export function tenantNeedsNoAdminWarning(tenant: TenantListItemForSwitcher): boolean {
    return (
        tenant.status === 'active' &&
        tenant.isActive &&
        !tenant.adminEmail
    );
}
