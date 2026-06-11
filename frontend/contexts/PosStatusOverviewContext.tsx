import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from 'react';

import { licenseApi } from '../api/license';
import { applyPersistedLicenseOverride } from '../utils/posLicenseLocalOverride';
import {
  deriveMandantWarningFlags,
  mapOverviewLicenseToStatus,
  mapOverviewToMandantWarning,
} from '../utils/mapPosStatusOverview';
import { fetchPosStatusOverview } from '../services/api/posStatusOverviewService';
import type { PosStatusOverviewDto } from '../services/api/posStatusOverviewTypes';
import type { PosCashRegisterContextDto } from '../utils/posCashRegisterReadinessParse';
import {
  getCachedLicenseStatus,
  setCachedLicenseStatus,
  type LicenseStatus,
} from '../services/license/licenseStatusCache';
import { registerPosStatusOverviewRefresh } from '../services/pos/posStatusOverviewRefreshBridge';
import { subscribePosStatusReconnectRefresh } from '../services/pos/posStatusOverviewSyncNotifier';
import type { MandantLicenseWarningState } from '../types/mandantLicenseWarning';

import { useAuth } from './AuthContext';

type PosStatusOverviewContextValue = {
  overview: PosStatusOverviewDto | null;
  licenseStatus: LicenseStatus | null;
  mandantWarning: MandantLicenseWarningState | null;
  shouldShowGrace: boolean;
  shouldShowPreExpiry: boolean;
  cashRegister: PosCashRegisterContextDto | null;
  settingsCashRegisterId: string | null;
  settingsVersion: number;
  loading: boolean;
  refreshOverview: (force?: boolean) => Promise<void>;
};

const PosStatusOverviewContext = createContext<PosStatusOverviewContextValue | null>(null);

let cachedOverview: PosStatusOverviewDto | null = null;

export function getCachedPosSettingsSnapshot(): {
  cashRegisterId: string | null;
  settingsVersion: number;
} | null {
  if (!cachedOverview) return null;
  return {
    cashRegisterId: cachedOverview.settings.cashRegisterId,
    settingsVersion: cachedOverview.settings.settingsVersion,
  };
}

async function licenseFromAnonymousStatus(): Promise<LicenseStatus | null> {
  try {
    const pub = await licenseApi.getStatus();
    const merged = mapOverviewLicenseToStatus(pub, {
      isValid: pub.isValid === true,
      isTrial: (pub.mode ?? '').toLowerCase() === 'trial',
      isExpired: pub.isExpired === true,
      daysRemaining: pub.daysRemaining,
      expiryDate: pub.validUntil,
      machineHash: '',
    });
    return applyPersistedLicenseOverride(merged);
  } catch {
    return getCachedLicenseStatus();
  }
}

/**
 * Single GET /api/pos/status/overview for authenticated POS sessions.
 * License refresh: app start (once), network reconnect, and explicit payment gate only.
 */
export function PosStatusOverviewProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const [overview, setOverview] = useState<PosStatusOverviewDto | null>(cachedOverview);
  const [licenseStatus, setLicenseStatus] = useState<LicenseStatus | null>(() => getCachedLicenseStatus());
  const [mandantWarning, setMandantWarning] = useState<MandantLicenseWarningState | null>(null);
  const [loading, setLoading] = useState(false);

  const applyOverview = useCallback(async (next: PosStatusOverviewDto) => {
    cachedOverview = next;
    const license = await applyPersistedLicenseOverride(
      mapOverviewLicenseToStatus(next.license, next.healthLicense),
    );
    setCachedLicenseStatus(license);
    setOverview(next);
    setLicenseStatus(license);
    setMandantWarning(mapOverviewToMandantWarning(next.license));
  }, []);

  const refreshOverview = useCallback(
    async (force = false) => {
      if (!isAuthenticated || !user?.id) {
        if (!force) return;
        const anon = await licenseFromAnonymousStatus();
        if (anon) {
          setCachedLicenseStatus(anon);
          setLicenseStatus(anon);
        }
        return;
      }

      setLoading(true);
      try {
        const next = await fetchPosStatusOverview();
        await applyOverview(next);
      } catch {
        if (getCachedLicenseStatus()) {
          setLicenseStatus(getCachedLicenseStatus());
        }
      } finally {
        setLoading(false);
      }
    },
    [applyOverview, isAuthenticated, user?.id],
  );

  useEffect(() => {
    void refreshOverview(false);
  }, [isAuthenticated, user?.id, refreshOverview]);

  useEffect(() => registerPosStatusOverviewRefresh(refreshOverview), [refreshOverview]);

  useEffect(() => {
    if (!isAuthenticated) return;
    return subscribePosStatusReconnectRefresh(() => {
      void refreshOverview(true);
    });
  }, [isAuthenticated, refreshOverview]);

  const { shouldShowGrace, shouldShowPreExpiry } = useMemo(
    () => deriveMandantWarningFlags(mandantWarning),
    [mandantWarning],
  );

  const value = useMemo<PosStatusOverviewContextValue>(
    () => ({
      overview,
      licenseStatus,
      mandantWarning,
      shouldShowGrace,
      shouldShowPreExpiry,
      cashRegister: overview?.cashRegister ?? null,
      settingsCashRegisterId: overview?.settings.cashRegisterId ?? null,
      settingsVersion: overview?.settings.settingsVersion ?? 0,
      loading,
      refreshOverview,
    }),
    [
      overview,
      licenseStatus,
      mandantWarning,
      shouldShowGrace,
      shouldShowPreExpiry,
      loading,
      refreshOverview,
    ],
  );

  return (
    <PosStatusOverviewContext.Provider value={value}>{children}</PosStatusOverviewContext.Provider>
  );
}

export function usePosStatusOverview(): PosStatusOverviewContextValue {
  const ctx = useContext(PosStatusOverviewContext);
  if (!ctx) {
    throw new Error('usePosStatusOverview must be used within PosStatusOverviewProvider');
  }
  return ctx;
}
