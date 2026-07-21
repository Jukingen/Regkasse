'use client';

import {
  type ReactNode,
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';

import { TenantSwitchOverlay } from '@/components/ui/TenantSwitchOverlay';
import {
  beginTenantSwitch as beginTenantSwitchGlobal,
  endTenantSwitch as endTenantSwitchGlobal,
  subscribeTenantSwitch,
} from '@/features/auth/services/tenantSwitchController';

const TENANT_SWITCH_OVERLAY_TIMEOUT_MS = 1000;

type TenantSwitchContextValue = {
  isTenantSwitching: boolean;
  beginTenantSwitch: () => void;
  endTenantSwitch: () => void;
};

const TenantSwitchContext = createContext<TenantSwitchContextValue | null>(null);

export function TenantSwitchProvider({ children }: { children: ReactNode }) {
  const [isTenantSwitching, setIsTenantSwitching] = useState(false);

  useEffect(() => subscribeTenantSwitch(setIsTenantSwitching), []);

  const beginTenantSwitch = useCallback(() => {
    beginTenantSwitchGlobal();
  }, []);

  const endTenantSwitch = useCallback(() => {
    endTenantSwitchGlobal();
  }, []);

  useEffect(() => {
    if (!isTenantSwitching) {
      return;
    }
    const timeoutId = window.setTimeout(() => {
      endTenantSwitchGlobal();
    }, TENANT_SWITCH_OVERLAY_TIMEOUT_MS);
    return () => window.clearTimeout(timeoutId);
  }, [isTenantSwitching]);

  const value = useMemo(
    () => ({ isTenantSwitching, beginTenantSwitch, endTenantSwitch }),
    [isTenantSwitching, beginTenantSwitch, endTenantSwitch]
  );

  return (
    <TenantSwitchContext.Provider value={value}>
      {children}
      <TenantSwitchOverlay visible={isTenantSwitching} />
    </TenantSwitchContext.Provider>
  );
}

export function useTenantSwitch(): TenantSwitchContextValue {
  const ctx = useContext(TenantSwitchContext);
  if (!ctx) {
    throw new Error('useTenantSwitch must be used within TenantSwitchProvider');
  }
  return ctx;
}
