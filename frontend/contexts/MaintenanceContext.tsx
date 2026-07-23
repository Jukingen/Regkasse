import React, { createContext, useContext, useEffect, useMemo, useState } from 'react';

import { useAuth } from './AuthContext';
import {
  checkMaintenanceStatus,
  type MaintenanceModeStatusDto,
} from '../services/api/maintenanceModeService';

type MaintenanceContextValue = {
  status: MaintenanceModeStatusDto | null;
  /** When true, POS payment submit must be blocked. */
  isBlocking: boolean;
  refresh: () => Promise<void>;
};

const MaintenanceContext = createContext<MaintenanceContextValue | null>(null);

const POLL_MS = 60_000;

/**
 * Platform maintenance mode for POS.
 * Blocks payments only when the API reports an active window — never on transient network errors.
 */
export function MaintenanceProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [status, setStatus] = useState<MaintenanceModeStatusDto | null>(null);

  const refresh = async () => {
    if (!isAuthenticated) {
      setStatus(null);
      return;
    }
    try {
      const data = await checkMaintenanceStatus();
      setStatus(data);
    } catch {
      // Keep last known status; do not assume maintenance on network failure.
    }
  };

  useEffect(() => {
    void refresh();
    if (!isAuthenticated) return;
    const id = setInterval(() => void refresh(), POLL_MS);
    return () => clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refresh closes over isAuthenticated
  }, [isAuthenticated]);

  const isBlocking = Boolean(status?.isActive && status.blocksPosPayments);

  const value = useMemo(
    () => ({
      status,
      isBlocking,
      refresh,
    }),
    [status, isBlocking]
  );

  return <MaintenanceContext.Provider value={value}>{children}</MaintenanceContext.Provider>;
}

export function useMaintenance(): MaintenanceContextValue {
  const ctx = useContext(MaintenanceContext);
  if (!ctx) {
    throw new Error('useMaintenance must be used within MaintenanceProvider');
  }
  return ctx;
}
