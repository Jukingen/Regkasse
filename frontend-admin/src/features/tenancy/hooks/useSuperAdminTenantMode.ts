'use client';

import { useMemo } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { normalizeAdminPathname } from '@/shared/adminSidebarNavigation';

/** Routes reachable on platform admin host without mandant context (tenant pick / platform ops). */
export const SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES = [
    '/admin/tenants',
    '/admin/users',
    '/admin/licenses',
    '/admin/license',
    '/admin/system',
] as const;

export function isPathAllowedWithoutTenant(pathname: string | null | undefined): boolean {
    const p = normalizeAdminPathname(pathname);
    if (p === '/admin') {
        return true;
    }
    return SUPER_ADMIN_PLATFORM_ALLOWED_PREFIXES.some(
        (prefix) => p === prefix || p.startsWith(`${prefix}/`),
    );
}

/**
 * Super Admin on platform host (`admin.*`) without impersonation / dev tenant override.
 */
export function useSuperAdminTenantMode() {
    const { user } = useAuth();
    const ctx = useTenantContext();

    return useMemo(() => {
        const isSuperAdminUser = isSuperAdmin(user?.role);

        const isPlatformAdminHost = ctx.hostSlug === 'admin';

        const hasActiveTenantContext =
            ctx.isImpersonating ||
            ctx.isDevTenantOverride ||
            (!isPlatformAdminHost && ctx.tenantSlug !== 'admin');

        const requiresTenantSelection =
            isSuperAdminUser && isPlatformAdminHost && !hasActiveTenantContext;

        const isSuperAdminPlatformMode = requiresTenantSelection;

        return {
            ...ctx,
            isSuperAdminUser,
            hasActiveTenantContext,
            requiresTenantSelection,
            isSuperAdminPlatformMode,
        };
    }, [user?.role, ctx]);
}
