'use client';

import { useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  type AdminTenantListItem,
  listAdminTenants,
} from '@/features/super-admin/api/adminTenants';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { isTestTenantSlug } from '@/features/tenancy/hooks/useTenantListForSwitcher';
import { isBusinessTenantSlug } from '@/features/users/utils/userScope';

export type UseTenantListOptions = {
  enabled?: boolean;
};

/** Active business mandants for pickers — hides platform and leftover Test Cafe / Test Bar. */
export function isOperationalPickerTenant(row: Pick<AdminTenantListItem, 'isActive' | 'slug'>): boolean {
  return row.isActive && isBusinessTenantSlug(row.slug) && !isTestTenantSlug(row.slug);
}

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
      .filter((row) => isOperationalPickerTenant(row))
      .sort((a, b) => a.name.localeCompare(b.name, 'de'));
  }, [superAdmin, adminTenantsQuery.data, switcherTenantsQuery.data]);

  const isLoading = superAdmin ? adminTenantsQuery.isLoading : switcherTenantsQuery.isLoading;

  return {
    tenants,
    isLoading,
    isSuperAdmin: superAdmin,
  };
}
