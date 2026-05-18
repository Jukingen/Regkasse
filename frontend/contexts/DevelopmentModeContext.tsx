import React, { createContext, useContext, useMemo } from 'react';

import { useDevelopmentMode } from '../hooks/useDevelopmentMode';
import { useConnectivity } from '../hooks/useConnectivity';
import type { DevelopmentModeSettings } from '../services/developmentModeClientCache';

type DevelopmentModeContextValue = {
  settings: DevelopmentModeSettings | null;
  isLoading: boolean;
  refetch: () => Promise<void>;
};

const DevelopmentModeContext = createContext<DevelopmentModeContextValue | null>(null);

/**
 * Single poll of `/system/development-mode` (axios base includes `/api`) for the whole POS tree + connectivity override.
 */
export function DevelopmentModeProvider({ children }: { children: React.ReactNode }) {
  const { settings, isLoading, refetch } = useDevelopmentMode();
  useConnectivity({ forceOnline: settings?.forceOnline === true });

  const value = useMemo(
    () => ({ settings, isLoading, refetch }),
    [settings, isLoading, refetch],
  );

  return <DevelopmentModeContext.Provider value={value}>{children}</DevelopmentModeContext.Provider>;
}

export function useDevelopmentModeContext(): DevelopmentModeContextValue {
  const ctx = useContext(DevelopmentModeContext);
  if (!ctx) {
    throw new Error('useDevelopmentModeContext must be used within DevelopmentModeProvider');
  }
  return ctx;
}
