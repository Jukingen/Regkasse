'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { isSuperAdmin } from '@/features/auth/constants/roles';
import { listAdminTenants, type AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';

export type UseTenantListOptions = {
    enabled?: boolean;
};

/**
 * Active business tenants for invite modals and pickers.
 * Super Admin: full list from GET /api/admin/tenants; others: switcher memberships.
 */
export function useTenantList(options?: UseTenantListOptions) {
    const { user } = useAuth();
    const enabled = options?.enabled !== false;
    const superAdmin = isSuperAdmin(user?.role);

    const adminTenantsQuery = useQuery({
        queryKey: ['admin', 'tenants', false],
        queryFn: () => listAdminTenants(false),
        enabled: enabled && superAdmin,
    });

    const switcherTenantsQuery = useGetApiAdminTenants(undefined, {
        enabled: enabled && !superAdmin,
    });

    const tenants = useMemo((): AdminTenantListItem[] => {
        const rows = superAdmin ? (adminTenantsQuery.data ?? []) : (switcherTenantsQuery.data ?? []);
        return rows
            .filter((row) => row.isActive && isBusinessTenantSlug(row.slug))
            .sort((a, b) => a.name.localeCompare(b.name, 'de'));
    }, [superAdmin, adminTenantsQuery.data, switcherTenantsQuery.data]);

    const isLoading = superAdmin ? adminTenantsQuery.isLoading : switcherTenantsQuery.isLoading;

    return {
        tenants,
        isLoading,
        isSuperAdmin: superAdmin,
    };
}
