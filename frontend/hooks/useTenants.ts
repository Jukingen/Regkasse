import { useCallback, useEffect, useRef, useState } from 'react';
import { AppState, type AppStateStatus } from 'react-native';

import { isSameDevTenantPreset } from '../constants/devTenantCatalog';
import { getDevTenantSlugOverride, setDevTenantSlugOverride } from '../services/tenant/devTenant';
import type { TenantSwitcherListItem } from '../services/tenant/tenantSwitcherApi';
import { fetchFreshTenants } from '../services/tenant/tenantStorage';

export type UseTenantsOptions = {
  /** When false, skips fetch (e.g. not logged in). */
  enabled?: boolean;
};

async function reconcileStaleDevTenantOverride(rows: TenantSwitcherListItem[]): Promise<void> {
  const override = await getDevTenantSlugOverride();
  if (!override || rows.length === 0) return;

  const stillValid = rows.some((row) => isSameDevTenantPreset(row.slug, override));
  if (!stillValid) {
    await setDevTenantSlugOverride(null);
  }
}

/**
 * Dev tenant switcher: GET /api/tenants/switcher with foreground refresh and offline cache fallback.
 */
export function useTenants(options?: UseTenantsOptions) {
  const enabled = __DEV__ && (options?.enabled ?? true);
  const [tenants, setTenants] = useState<TenantSwitcherListItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isError, setIsError] = useState(false);
  const [isFromCache, setIsFromCache] = useState(false);
  const inFlightRef = useRef(false);

  const refreshTenants = useCallback(async () => {
    if (!enabled) {
      setTenants([]);
      setIsError(false);
      setIsFromCache(false);
      setIsLoading(false);
      return;
    }

    if (inFlightRef.current) return;
    inFlightRef.current = true;
    setIsLoading(true);

    try {
      const { tenants: rows, fromCache } = await fetchFreshTenants();
      setTenants(rows);
      setIsFromCache(fromCache);
      if (rows.length > 0) {
        setIsError(false);
        await reconcileStaleDevTenantOverride(rows);
      } else if (fromCache) {
        setIsError(false);
      } else {
        setIsError(true);
      }
    } catch {
      setTenants([]);
      setIsError(true);
      setIsFromCache(false);
    } finally {
      setIsLoading(false);
      inFlightRef.current = false;
    }
  }, [enabled]);

  useEffect(() => {
    void refreshTenants();
  }, [refreshTenants]);

  useEffect(() => {
    if (!enabled) return;

    const onAppStateChange = (nextState: AppStateStatus) => {
      if (nextState === 'active') {
        void refreshTenants();
      }
    };

    const subscription = AppState.addEventListener('change', onAppStateChange);
    return () => subscription.remove();
  }, [enabled, refreshTenants]);

  return {
    tenants,
    isLoading,
    isError,
    isFromCache,
    refreshTenants,
    tenantCount: tenants.length,
  };
}

/** @deprecated Prefer {@link useTenants}. */
export const useTenantSwitcherList = useTenants;
