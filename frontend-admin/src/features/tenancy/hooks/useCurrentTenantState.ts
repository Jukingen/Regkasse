'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';

import type { AdminTenantListItem } from '@/features/super-admin/api/adminTenants';
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
import { resolveActiveTenantId } from '@/features/tenancy/utils/resolveActiveTenantIdentity';

export type CurrentTenant = {
    tenantSlug: string | null | undefined;
    tenantId: string | null;
    tenantName: string | null;
    tenantStatus: string | null;
    isActive: boolean;
    isTenantSuspended: boolean;
    licenseValidUntilUtc: string | null;
    licenseKey: string | null;
    licenseDaysRemaining: number | null;
    resolvedTenant: AdminTenantListItem | null;
    displayLabel: string | null;
    hasAuthToken: boolean;
    isImpersonating: boolean;
    isDevTenantOverride: boolean;
    isPlatformAdminHost: boolean;
    hostSlug: string;
    requiresTenantSelection: boolean;
    isSuperAdminPlatformMode: boolean;
    isSuperAdminUser: boolean;
    isRealTenantSlug: boolean;
    showTenantLicenseInHeader: boolean;
    suppressLicenseWarnings: boolean;
    isTenantRecordLoading: boolean;
};

/** Resolves active mandant (header switcher / JWT / dev override). Used by {@link TenantProvider}. */
export function useCurrentTenantState(): CurrentTenant {
    const { user } = useAuth();
    const ctx = useTenantContext();
    const mode = useSuperAdminTenantMode();

    const switcherQuery = useGetApiAdminTenants(
        { includeDeleted: false },
        {
            enabled: ctx.hasAuthToken,
            staleTime: 60_000,
            refetchOnMount: true,
            refetchOnWindowFocus: true,
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
        const tenantId = resolveActiveTenantId({
            resolvedRowId: resolvedRow?.id,
            jwtTenantId,
            jwtTenantSlug,
            activeTenantSlug: tenantSlug,
        });
        const tenantName = resolvedRow?.name ?? ctx.tenantName;
        const tenantStatus = resolvedRow?.status ?? null;
        const isActive = resolvedRow?.isActive ?? true;
        const licenseValidUntilUtc = resolvedRow?.licenseValidUntilUtc ?? null;
        const licenseKey = resolvedRow?.licenseKey ?? null;
        const licenseDaysRemaining = resolvedRow?.licenseDaysRemaining ?? null;
        const isTenantSuspended = resolvedRow ? isTenantSuspendedOrInactive(resolvedRow) : false;

        const isSuperAdminUser = isSuperAdmin(user?.role);
        const isRealTenantSlug = Boolean(tenantSlug && tenantSlug !== 'admin');
        const isManager = user?.role === 'Manager';

        const showTenantLicenseInHeader =
            ctx.hasAuthToken && !isSuperAdminUser && isManager && isRealTenantSlug;

        const suppressLicenseWarnings = isSuperAdminUser;

        const awaitingTenantId =
            Boolean(tenantSlug && tenantSlug !== 'admin' && !tenantId);

        const isTenantRecordLoading =
            ctx.hasAuthToken
            && awaitingTenantId
            && (switcherQuery.isLoading || switcherQuery.isFetching);

        return {
            tenantSlug,
            tenantId,
            tenantName,
            tenantStatus,
            isActive,
            isTenantSuspended,
            licenseValidUntilUtc,
            licenseKey,
            licenseDaysRemaining,
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
