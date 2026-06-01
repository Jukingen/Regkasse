'use client';

import { useAntdApp } from '@/hooks/useAntdApp';

import { useQueryClient } from '@tanstack/react-query';

import { useCallback, useEffect, useRef } from 'react';

import {
    DEV_TENANT_CHANGED_EVENT,
    DEV_TENANT_LOCAL_STORAGE_KEY,
    type DevTenantChangedDetail,
    getTenantSlugFromSubdomain,
    isDevelopment,
} from '@/features/auth/services/devTenant';
import {
    beginTenantSwitch,
    endTenantSwitch,
} from '@/features/auth/services/tenantSwitchController';
import { authStorage } from '@/features/auth/services/authStorage';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { useI18n } from '@/i18n';

/** Unblocks tenant switch if `location.reload()` does not navigate away. */
export const TENANT_SWITCH_RELOAD_SAFETY_MS = 5_000;

function normalizeTenantSlug(value: string | null | undefined): string | null {
    const trimmed = value?.trim().toLowerCase();
    return trimmed && trimmed.length > 0 ? trimmed : null;
}

/**
 * Clears all TanStack Query caches, notifies the user, and reloads so axios / tenant hooks
 * pick up the new mandant (dev localStorage, cross-tab sync, or host slug change).
 */
export function useTenantChangeListener() {
  const { message } = useAntdApp();

    const queryClient = useQueryClient();
    const { t } = useI18n();
    const lastDevSlugRef = useRef<string | null>(null);
    const lastHostSlugRef = useRef<string | null>(null);
    const handlingRef = useRef(false);
    const reloadSafetyTimerRef = useRef<number | null>(null);

    const clearReloadSafetyTimer = useCallback(() => {
        if (reloadSafetyTimerRef.current != null) {
            clearTimeout(reloadSafetyTimerRef.current);
            reloadSafetyTimerRef.current = null;
        }
    }, []);

    const resetSwitchGuard = useCallback(() => {
        clearReloadSafetyTimer();
        if (handlingRef.current) {
            handlingRef.current = false;
            endTenantSwitch();
        }
    }, [clearReloadSafetyTimer]);

    const applyTenantSwitch = useCallback(
        (newTenantSlug: string) => {
            if (handlingRef.current) {
                return;
            }
            const normalized = normalizeTenantSlug(newTenantSlug);
            if (!normalized) {
                return;
            }

            const devStored = isDevelopment()
                ? normalizeTenantSlug(
                      typeof window !== 'undefined'
                          ? window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY)
                          : null,
                  )
                : null;
            const hostSlug = normalizeTenantSlug(getTenantSlugFromSubdomain()) ?? 'admin';

            if (isDevelopment() && devStored && devStored === normalized) {
                lastDevSlugRef.current = devStored;
            }
            lastHostSlugRef.current = hostSlug;

            clearReloadSafetyTimer();
            handlingRef.current = true;
            beginTenantSwitch();
            tenantStorage.persistBootstrap({ tenantSlug: normalized });
            // Drop JWT so login re-issues tenant_id for the selected mandant (dev header is not enough alone).
            authStorage.removeToken();
            queryClient.clear();
            message.info(t('adminShell.tenant.switch.toast', { slug: normalized }));

            if (typeof window === 'undefined') {
                resetSwitchGuard();
                return;
            }

            // Safety: reset handlingRef if reload fails to complete (e.g. beforeunload blocked).
            // On success, reload clears timers; unmount cleanup covers tests / HMR.
            reloadSafetyTimerRef.current = window.setTimeout(() => {
                reloadSafetyTimerRef.current = null;
                if (handlingRef.current) {
                    handlingRef.current = false;
                    endTenantSwitch();
                    console.warn('Tenant switch timeout - resetting state');
                }
            }, TENANT_SWITCH_RELOAD_SAFETY_MS);

            try {
                window.location.reload();
            } catch {
                resetSwitchGuard();
            }
        },
        [clearReloadSafetyTimer, queryClient, resetSwitchGuard, t],
    );

    useEffect(() => {
        if (typeof window === 'undefined') {
            return;
        }

        lastDevSlugRef.current = normalizeTenantSlug(
            window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY),
        );
        lastHostSlugRef.current = normalizeTenantSlug(getTenantSlugFromSubdomain());

        const handleStorageChange = (e: StorageEvent) => {
            if (e.key !== DEV_TENANT_LOCAL_STORAGE_KEY) {
                return;
            }
            if (e.newValue === e.oldValue) {
                return;
            }
            const next = normalizeTenantSlug(e.newValue);
            if (!next || next === lastDevSlugRef.current) {
                return;
            }
            applyTenantSwitch(next);
        };

        const handleDevTenantChanged = (e: Event) => {
            const detail = (e as CustomEvent<DevTenantChangedDetail>).detail;
            const next = normalizeTenantSlug(detail?.slug);
            if (!next || next === lastDevSlugRef.current) {
                return;
            }
            applyTenantSwitch(next);
        };

        const checkHostSlugChange = () => {
            const hostSlug = normalizeTenantSlug(getTenantSlugFromSubdomain());
            const prev = lastHostSlugRef.current;
            if (hostSlug && prev && hostSlug !== prev) {
                applyTenantSwitch(hostSlug);
                return;
            }
            if (hostSlug) {
                lastHostSlugRef.current = hostSlug;
            }
        };

        const syncDevSlugFromStorage = () => {
            if (!isDevelopment()) {
                return;
            }
            const current = normalizeTenantSlug(
                window.localStorage.getItem(DEV_TENANT_LOCAL_STORAGE_KEY),
            );
            const prev = lastDevSlugRef.current;
            if (current && prev && current !== prev) {
                applyTenantSwitch(current);
                return;
            }
            if (current) {
                lastDevSlugRef.current = current;
            }
        };

        const onVisibilityOrFocus = () => {
            syncDevSlugFromStorage();
            checkHostSlugChange();
        };

        window.addEventListener('storage', handleStorageChange);
        window.addEventListener(DEV_TENANT_CHANGED_EVENT, handleDevTenantChanged);
        window.addEventListener('pageshow', onVisibilityOrFocus);
        window.addEventListener('focus', onVisibilityOrFocus);
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible') {
                onVisibilityOrFocus();
            }
        });

        return () => {
            clearReloadSafetyTimer();
            window.removeEventListener('storage', handleStorageChange);
            window.removeEventListener(DEV_TENANT_CHANGED_EVENT, handleDevTenantChanged);
            window.removeEventListener('pageshow', onVisibilityOrFocus);
            window.removeEventListener('focus', onVisibilityOrFocus);
        };
    }, [applyTenantSwitch, clearReloadSafetyTimer]);
}
