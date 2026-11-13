'use client';

/**
 * FA mandant hook: React context from {@link TenantProvider} plus optional tenant list for pickers.
 *
 * Persistence uses `tenantStorage` (`rk_admin_tenant_*`), not a parallel `selectedTenant` key.
 * `X-Tenant-Id` is applied per request via {@link resolveTenantSlugForApiRequest} — never axios defaults.
 */
import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import {
  type Tenant,
  type TenantContextType,
  useTenant as useTenantFromProvider,
} from '@/features/tenancy/providers/TenantProvider';

export type { Tenant, TenantContextType };

export type UseTenantOptions = {
  /** When true, load active tenants for Super Admin picker UIs (TenantGuard). */
  loadTenants?: boolean;
};

export type UseTenantResult = TenantContextType & {
  /** Active business tenants for Super Admin / membership switcher pickers. */
  tenants: AdminTenantListItem[];
  tenantsLoading: boolean;
};

export function useTenant(options?: UseTenantOptions): UseTenantResult {
  const base = useTenantFromProvider();
  const { user } = useAuth();
  const loadTenants = options?.loadTenants === true;
  const { tenants, isLoading: tenantsLoading } = useTenantList({
    enabled: loadTenants && Boolean(user) && isSuperAdmin(user?.role),
  });

  return useMemo(
    () => ({
      ...base,
      tenants,
      tenantsLoading,
    }),
    [base, tenants, tenantsLoading]
  );
}
