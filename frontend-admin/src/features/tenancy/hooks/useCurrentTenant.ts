'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { getDevTenant, isDevelopment } from '@/features/auth/services/devTenant';
import { readTokenTenantClaims } from '@/features/auth/services/tokenTenantClaims';
import { useGetApiAdminTenants } from '@/features/tenancy/api/getApiAdminTenants';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import { useSuperAdminTenantMode } from '@/features/tenancy/hooks/useSuperAdminTenantMode';
import {
    isTenantSuspendedOrInactive,
    resolveActiveTenantFromSwitcherList,
} from '@/features/super-admin/utils/tenantHeaderSwitcher';

/**
 * Header badge, dev switcher, and mandant-scoped pages: one resolved tenant row
 * (JWT tenant_id → dev localStorage slug → host subdomain).
 */
export function useCurrentTenant() {
    const { user } = useAuth();
    const ctx = useTenantContext();
    const mode = useSuperAdminTenantMode();

    const switcherQuery = useGetApiAdminTenants(
        { includeDeleted: false },
        {
            enabled: ctx.hasAuthToken,
            staleTime: 60_000,
        },
    );

    return useMemo(() => {
        const tokenSnapshot = readTokenTenantClaims();
        const jwtTenantId =
            user?.tenantId ?? tokenSnapshot.tenantId ?? ctx.tenantId ?? null;
        const jwtTenantSlug = user?.tenantSlug ?? tokenSnapshot.tenantSlug ?? ctx.jwtTenantSlug ?? null;
        const rawDevSlug = isDevelopment() ? (ctx.devSelectedSlug ?? getDevTenant()) : null;
        const devTenantSlug =
            rawDevSlug && rawDevSlug !== 'admin' ? rawDevSlug : null;

        const resolvedRow = resolveActiveTenantFromSwitcherList(switcherQuery.data ?? [], {
            jwtTenantId,
            jwtTenantSlug,
            isImpersonating: ctx.isImpersonating,
            isDevTenantOverride: ctx.isDevTenantOverride,
            devTenantSlug,
            hostSlug: ctx.hostSlug,
        });

        const tenantSlug = resolvedRow?.slug ?? ctx.tenantSlug;
        const tenantId = resolvedRow?.id ?? jwtTenantId;
        const tenantName = resolvedRow?.name ?? ctx.tenantName;
        const tenantStatus = resolvedRow?.status ?? null;
        const isActive = resolvedRow?.isActive ?? true;
        const licenseValidUntilUtc = resolvedRow?.licenseValidUntilUtc ?? null;
        const licenseKey = resolvedRow?.licenseKey ?? null;
        const isTenantSuspended = resolvedRow ? isTenantSuspendedOrInactive(resolvedRow) : false;

        const isSuperAdminUser = isSuperAdmin(user?.role);
        const isRealTenantSlug = Boolean(tenantSlug && tenantSlug !== 'admin');
        const isManager = user?.role === 'Manager';

        const showTenantLicenseInHeader =
            ctx.hasAuthToken && !isSuperAdminUser && isManager && isRealTenantSlug;

        const suppressLicenseWarnings = isSuperAdminUser;

        const isTenantRecordLoading =
            ctx.hasAuthToken &&
            switcherQuery.isLoading &&
            switcherQuery.fetchStatus !== 'idle' &&
            !resolvedRow;

        return {
            tenantSlug,
            tenantId,
            tenantName,
            tenantStatus,
            isActive,
            isTenantSuspended,
            licenseValidUntilUtc,
            licenseKey,
            resolvedTenant: resolvedRow,
            displayLabel: tenantName ?? (tenantSlug !== 'admin' ? tenantSlug : null),
            hasAuthToken: ctx.hasAuthToken,
            isImpersonating: ctx.isImpersonating,
            isDevTenantOverride: ctx.isDevTenantOverride,
            isPlatformAdminHost: ctx.isPlatformAdminHost,
            hostSlug: ctx.hostSlug,
            requiresTenantSelection: mode.requiresTenantSelection,
            isSuperAdminPlatformMode: mode.isSuperAdminPlatformMode,
            isSuperAdminUser,
            isRealTenantSlug,
            showTenantLicenseInHeader,
            suppressLicenseWarnings,
            isTenantRecordLoading,
        };
    }, [
        user?.role,
        user?.tenantId,
        user?.tenantSlug,
        ctx.tenantSlug,
        ctx.tenantId,
        ctx.tenantName,
        ctx.jwtTenantSlug,
        ctx.hasAuthToken,
        ctx.isImpersonating,
        ctx.isDevTenantOverride,
        ctx.isPlatformAdminHost,
        ctx.hostSlug,
        mode.requiresTenantSelection,
        mode.isSuperAdminPlatformMode,
        switcherQuery.data,
        switcherQuery.isLoading,
        switcherQuery.fetchStatus,
    ]);
}
