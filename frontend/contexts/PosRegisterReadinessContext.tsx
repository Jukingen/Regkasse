// Cached POST /api/pos/cash-register/ensure-ready: effective register, nextAction, optional auto-open. Not invoked by payment POST.
import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { postEnsurePosCashRegisterReady } from '../services/api/posCashRegisterReadinessService';
import type { PosCashRegisterContextDto } from '../utils/posCashRegisterReadinessParse';

import { useAuth } from './AuthContext';
import { usePosStatusOverview } from './PosStatusOverviewContext';

export type PosRegisterReadinessContextValue = {
  data: PosCashRegisterContextDto | null;
  loading: boolean;
  error: Error | null;
  /** Re-run ensure-ready (e.g. after network recovery). */
  refresh: () => void;
  /**
   * Await a full ensure-ready round-trip (e.g. after PUT user cash-register) so payment gate does not read stale nextAction.
   */
  refreshAsync: () => Promise<void>;
};

const PosRegisterReadinessContext = createContext<PosRegisterReadinessContextValue>({
  data: null,
  loading: false,
  error: null,
  refresh: () => {},
  refreshAsync: async () => {},
});

export function usePosRegisterReadiness(): PosRegisterReadinessContextValue {
  return useContext(PosRegisterReadinessContext);
}

export function PosRegisterReadinessProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const { cashRegister: overviewRegister } = usePosStatusOverview();
  const overviewRegisterRef = useRef(overviewRegister);
  overviewRegisterRef.current = overviewRegister;
  const [data, setData] = useState<PosCashRegisterContextDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [token, setToken] = useState(0);
  const refreshWaitersRef = useRef<(() => void)[]>([]);
  const ensureReadyInFlightRef = useRef(false);

  const refresh = useCallback(() => {
    setToken((n) => n + 1);
  }, []);

  const refreshAsync = useCallback(
    () =>
      new Promise<void>((resolve) => {
        refreshWaitersRef.current.push(resolve);
        setToken((n) => n + 1);
      }),
    []
  );

  useEffect(() => {
    if (POS_ENSURE_READY_ON_ENTRY) return;
    if (!overviewRegister) return;
    setData(overviewRegister);
  }, [overviewRegister]);

  useEffect(() => {
    if (!POS_ENSURE_READY_ON_ENTRY || !isAuthenticated || !user?.id) {
      setData(null);
      setLoading(false);
      setError(null);
      const waiters = refreshWaitersRef.current;
      refreshWaitersRef.current = [];
      waiters.forEach((w) => w());
      return;
    }

    let cancelled = false;
    ensureReadyInFlightRef.current = true;
    setLoading(true);
    setError(null);
    setData(null);

    void (async () => {
      try {
        const dto = await postEnsurePosCashRegisterReady();
        if (!cancelled) {
          setData(dto);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e : new Error(String(e)));
          setData(overviewRegisterRef.current ?? null);
        }
      } finally {
        ensureReadyInFlightRef.current = false;
        if (!cancelled) {
          setLoading(false);
        }
        if (!cancelled) {
          const waiters = refreshWaitersRef.current;
          refreshWaitersRef.current = [];
          waiters.forEach((w) => w());
        }
      }
    })();

    return () => {
      cancelled = true;
      ensureReadyInFlightRef.current = false;
    };
  }, [isAuthenticated, user?.id, token]);

  const value = useMemo(
    () => ({
      data,
      loading,
      error,
      refresh,
      refreshAsync,
    }),
    [data, loading, error, refresh, refreshAsync]
  );

  return (
    <PosRegisterReadinessContext.Provider value={value}>{children}</PosRegisterReadinessContext.Provider>
  );
}
