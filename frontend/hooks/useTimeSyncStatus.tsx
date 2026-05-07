import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';

import { apiClient } from '../services/api/config';
import {
  deriveTimeSyncUiFlags,
  normalizeSystemTimeStatusDto,
  type SystemTimeStatusDto,
} from '../types/timeSyncStatus';

const POLL_MS = 5 * 60 * 1000;

export type TimeSyncStatusContextValue = {
  status: SystemTimeStatusDto | null;
  loading: boolean;
  error: unknown | null;
  refetch: () => Promise<void>;
  /** Convenience flags for banners and PaymentModal */
  absOffsetSeconds: number | null;
  timeSyncCritical: boolean;
  timeSyncWarningBand: boolean;
};

const TimeSyncStatusContext = createContext<TimeSyncStatusContextValue | null>(null);

export function TimeSyncStatusProvider({
  children,
  enabled,
}: {
  children: ReactNode;
  enabled: boolean;
}) {
  const [status, setStatus] = useState<SystemTimeStatusDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown | null>(null);

  const fetchStatus = useCallback(async () => {
    if (!enabled) return;
    setLoading(true);
    setError(null);
    try {
      const raw = await apiClient.get<unknown>('/system/time/status');
      const next = normalizeSystemTimeStatusDto(raw);
      setStatus(next);
    } catch (e) {
      setError(e);
      setStatus(null);
    } finally {
      setLoading(false);
    }
  }, [enabled]);

  useEffect(() => {
    if (!enabled) {
      setStatus(null);
      setError(null);
      return;
    }
    void fetchStatus();
    const id = setInterval(() => {
      void fetchStatus();
    }, POLL_MS);
    return () => clearInterval(id);
  }, [enabled, fetchStatus]);

  const flags = useMemo(() => deriveTimeSyncUiFlags(status), [status]);

  const value = useMemo<TimeSyncStatusContextValue>(
    () => ({
      status,
      loading,
      error,
      refetch: fetchStatus,
      absOffsetSeconds: flags.absOffsetSeconds,
      timeSyncCritical: flags.timeSyncCritical,
      timeSyncWarningBand: flags.timeSyncWarningBand,
    }),
    [status, loading, error, fetchStatus, flags]
  );

  return <TimeSyncStatusContext.Provider value={value}>{children}</TimeSyncStatusContext.Provider>;
}

export function useTimeSyncStatus(): TimeSyncStatusContextValue {
  const ctx = useContext(TimeSyncStatusContext);
  if (!ctx) {
    throw new Error('useTimeSyncStatus must be used within TimeSyncStatusProvider');
  }
  return ctx;
}
