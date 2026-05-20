'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { listAdminTenants } from '@/features/super-admin/api/adminTenants';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import { resolveTenantSlug } from '@/lib/tenantResolver';

const ADMIN_TENANTS_QUERY_KEY = ['admin', 'tenants', false] as const;

/**
 * Header badge and admin UI: slug from dev override / subdomain, id from JWT/storage,
 * display name from JWT or Super Admin tenant list when missing.
 */
export function useCurrentTenant() {
    const { user } = useAuth();
    const ctx = useTenantContext();
    const mode = useSuperAdminTenantMode();

    const shouldFetchName =
        isSuperAdmin(user?.role) &&
        !ctx.tenantName &&
        ctx.tenantSlug !== 'admin' &&
        ctx.hasAuthToken;

    const tenantsQuery = useQuery({
        queryKey: ADMIN_TENANTS_QUERY_KEY,
        queryFn: () => listAdminTenants(false),
        enabled: shouldFetchName,
        staleTime: 60_000,
    });

    return useMemo(() => {
        const tenantSlug = ctx.tenantSlug || resolveTenantSlug();
        const tenantId = ctx.tenantId;
        const tenantNameFromApi = tenantsQuery.data?.find((row) => row.slug === tenantSlug)?.name ?? null;
        const tenantName = ctx.tenantName ?? tenantNameFromApi;

        return {
            tenantSlug,
            tenantId,
            tenantName,
            displayLabel: tenantName ?? (tenantSlug !== 'admin' ? tenantSlug : null),
            hasAuthToken: ctx.hasAuthToken,
            isImpersonating: ctx.isImpersonating,
            isDevTenantOverride: ctx.isDevTenantOverride,
            isPlatformAdminHost: ctx.isPlatformAdminHost,
            hostSlug: ctx.hostSlug,
            requiresTenantSelection: mode.requiresTenantSelection,
            isSuperAdminPlatformMode: mode.isSuperAdminPlatformMode,
        };
    }, [
        ctx.tenantSlug,
        ctx.tenantId,
        ctx.tenantName,
        ctx.hasAuthToken,
        ctx.isImpersonating,
        ctx.isDevTenantOverride,
        ctx.isPlatformAdminHost,
        ctx.hostSlug,
        mode.requiresTenantSelection,
        mode.isSuperAdminPlatformMode,
        tenantsQuery.data,
    ]);
}
