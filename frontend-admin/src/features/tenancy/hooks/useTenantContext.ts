'use client';

import { useMemo, useSyncExternalStore } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import {
    DEV_TENANT_LOCAL_STORAGE_KEY,
    getDevTenant,
    getEffectiveTenantSlug,
    getTenantSlugFromSubdomain,
    isDevelopment,
} from '@/features/auth/services/devTenant';
import { readTokenTenantClaims } from '@/features/auth/services/tokenTenantClaims';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { authStorage } from '@/features/auth/services/authStorage';

const emptySubscribe = () => () => {};

function readDevTenantOverrideActive(): boolean {
    if (!isDevelopment() || typeof window === 'undefined') return false;
    const stored = window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)?.trim();
    if (!stored) return false;
    const hostSlug = getTenantSlugFromSubdomain();
    if (hostSlug !== 'admin' && stored === hostSlug) return false;
    return true;
}

/**
 * Resolved tenant context for header badge, Verwaltung info card, and dev switcher labels.
 */
export function useTenantContext() {
    const { user } = useAuth();

    const tokenSnapshot = useSyncExternalStore(
        emptySubscribe,
        () => readTokenTenantClaims(),
        () => ({ tenantId: null, tenantSlug: null, isImpersonating: false }),
    );

    const hostSlug = useSyncExternalStore(
        emptySubscribe,
        () => (typeof window !== 'undefined' ? getTenantSlugFromSubdomain() : 'admin'),
        () => 'admin',
    );

    const effectiveSlug = useSyncExternalStore(
        emptySubscribe,
        () => getEffectiveTenantSlug(),
        () => 'dev',
    );

    return useMemo(() => {
        const jwtTenantSlug = user?.tenantSlug ?? tokenSnapshot.tenantSlug;
        const tenantId = user?.tenantId ?? tokenSnapshot.tenantId ?? tenantStorage.getTenantId();
        const tenantName = user?.tenantDisplayName?.trim() || null;
        const isPlatformAdminHost = hostSlug === 'admin' && effectiveSlug === 'admin';
        const isDevTenantOverride = readDevTenantOverrideActive();
        const devSelectedSlug = isDevelopment() ? getDevTenant() : null;

        const displayLabel =
            tenantName ||
            (effectiveSlug !== 'admin' ? effectiveSlug : null) ||
            (isPlatformAdminHost ? null : effectiveSlug);

        return {
            tenantSlug: effectiveSlug,
            tenantName,
            tenantId,
            jwtTenantSlug,
            displayLabel,
            hostSlug,
            isPlatformAdminHost,
            isImpersonating: tokenSnapshot.isImpersonating,
            isDevTenantOverride,
            devSelectedSlug,
            hasAuthToken: authStorage.hasToken(),
        };
    }, [user, tokenSnapshot, hostSlug, effectiveSlug]);
}
