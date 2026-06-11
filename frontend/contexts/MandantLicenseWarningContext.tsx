import React, { createContext, useContext, useMemo } from 'react';

import type { MandantLicenseWarningState } from '../types/mandantLicenseWarning';

import { usePosStatusOverview } from './PosStatusOverviewContext';

export type { MandantLicenseWarningState };

type MandantLicenseWarningContextValue = {
  state: MandantLicenseWarningState | null;
  shouldShowGrace: boolean;
  shouldShowPreExpiry: boolean;
  refetch: () => Promise<void>;
};

const MandantLicenseWarningContext = createContext<MandantLicenseWarningContextValue | null>(null);

/** Mandant warning band data from combined overview (no separate polling). */
export function MandantLicenseWarningProvider({ children }: { children: React.ReactNode }) {
  const {
    mandantWarning,
    shouldShowGrace,
    shouldShowPreExpiry,
    refreshOverview,
  } = usePosStatusOverview();

  const value = useMemo(
    () => ({
      state: mandantWarning,
      shouldShowGrace,
      shouldShowPreExpiry,
      refetch: () => refreshOverview(true),
    }),
    [mandantWarning, shouldShowGrace, shouldShowPreExpiry, refreshOverview],
  );

  return (
    <MandantLicenseWarningContext.Provider value={value}>
      {children}
    </MandantLicenseWarningContext.Provider>
  );
}

export function useMandantLicenseWarning(): MandantLicenseWarningContextValue {
  const ctx = useContext(MandantLicenseWarningContext);
  if (!ctx) {
    throw new Error('useMandantLicenseWarning must be used within MandantLicenseWarningProvider');
  }
  return ctx;
}
