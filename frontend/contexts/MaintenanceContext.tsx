import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { useAuth } from './AuthContext';
import {
  checkMaintenanceStatus,
  endMaintenance,
  type MaintenanceModeStatusDto,
} from '../services/api/maintenanceModeService';

type MaintenanceContextValue = {
  status: MaintenanceModeStatusDto | null;
  /** When true, POS payment submit must be blocked (never true for Super Admin). */
  isBlocking: boolean;
  isSuperAdmin: boolean;
  /** Super Admin can continue operating while maintenance is active. */
  canBypass: boolean;
  isDisabling: boolean;
  refresh: () => Promise<void>;
  /** Super Admin only — ends platform maintenance mode. */
  disableMaintenance: () => Promise<void>;
};

const MaintenanceContext = createContext<MaintenanceContextValue | null>(null);

const POLL_MS = 60_000;

function resolveIsSuperAdmin(user: {
  role?: string | null;
  roles?: string[] | null;
} | null): boolean {
  if (!user) return false;
  return user.role === 'SuperAdmin' || user.roles?.includes('SuperAdmin') === true;
}

/**
 * Platform maintenance mode for POS.
 * Blocks payments only when the API reports an active window — never on transient network errors.
 * Super Admin always bypasses payment blocks (aligned with MaintenanceMiddleware).
 */
export function MaintenanceProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const [status, setStatus] = useState<MaintenanceModeStatusDto | null>(null);
  const [isDisabling, setIsDisabling] = useState(false);

  const isSuperAdmin = resolveIsSuperAdmin(user);
  const canBypass = isSuperAdmin;

  const refresh = useCallback(async () => {
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
  }, [isAuthenticated]);

  useEffect(() => {
    void refresh();
    if (!isAuthenticated) return;
    const id = setInterval(() => void refresh(), POLL_MS);
    return () => clearInterval(id);
  }, [isAuthenticated, refresh]);

  const disableMaintenance = useCallback(async () => {
    if (!isSuperAdmin) return;
    setIsDisabling(true);
    try {
      const data = await endMaintenance();
      setStatus(data);
    } finally {
      setIsDisabling(false);
    }
  }, [isSuperAdmin]);

  const isBlocking = Boolean(status?.isActive && status.blocksPosPayments && !canBypass);

  const value = useMemo(
    () => ({
      status,
      isBlocking,
      isSuperAdmin,
      canBypass,
      isDisabling,
      refresh,
      disableMaintenance,
    }),
    [status, isBlocking, isSuperAdmin, canBypass, isDisabling, refresh, disableMaintenance]
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
