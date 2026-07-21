import React, { createContext, useCallback, useContext, useMemo } from 'react';

import { usePosStatusOverview } from './PosStatusOverviewContext';
import type { LicenseStatus } from '../services/license/licenseStatusCache';

export type { LicenseStatus };

type LicenseStatusContextValue = {
  status: LicenseStatus | null;
  loading: boolean;
  /** Pass `true` to bypass caches (e.g. after activation). */
  refetch: (force?: boolean) => Promise<void>;
};

const LicenseStatusContext = createContext<LicenseStatusContextValue | null>(null);

/** License read model fed by {@link PosStatusOverviewProvider} (no background polling). */
export function LicenseStatusProvider({ children }: { children: React.ReactNode }) {
  const { licenseStatus, loading, refreshOverview } = usePosStatusOverview();

  const refetch = useCallback((force = true) => refreshOverview(force), [refreshOverview]);

  const value = useMemo(
    () => ({ status: licenseStatus, loading, refetch }),
    [licenseStatus, loading, refetch]
  );

  return <LicenseStatusContext.Provider value={value}>{children}</LicenseStatusContext.Provider>;
}

export function useLicenseStatus(): LicenseStatusContextValue {
  const ctx = useContext(LicenseStatusContext);
  if (!ctx) {
    throw new Error('useLicenseStatus must be used within LicenseStatusProvider');
  }
  return ctx;
}
