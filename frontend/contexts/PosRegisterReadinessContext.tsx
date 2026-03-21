// Cached POST /api/pos/cash-register/ensure-ready: effective register, nextAction, optional auto-open. Not invoked by payment POST.
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';

import { POS_ENSURE_READY_ON_ENTRY } from '../constants/posFeatureFlags';
import { postEnsurePosCashRegisterReady } from '../services/api/posCashRegisterReadinessService';
import type { PosCashRegisterContextDto } from '../utils/posCashRegisterReadinessParse';

import { useAuth } from './AuthContext';

export type PosRegisterReadinessContextValue = {
  data: PosCashRegisterContextDto | null;
  loading: boolean;
  error: Error | null;
  /** Re-run ensure-ready (e.g. after network recovery). */
  refresh: () => void;
};

const PosRegisterReadinessContext = createContext<PosRegisterReadinessContextValue>({
  data: null,
  loading: false,
  error: null,
  refresh: () => {},
});

export function usePosRegisterReadiness(): PosRegisterReadinessContextValue {
  return useContext(PosRegisterReadinessContext);
}

export function PosRegisterReadinessProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, user } = useAuth();
  const [data, setData] = useState<PosCashRegisterContextDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error | null>(null);
  const [token, setToken] = useState(0);

  const refresh = useCallback(() => {
    setToken((n) => n + 1);
  }, []);

  useEffect(() => {
    if (!POS_ENSURE_READY_ON_ENTRY || !isAuthenticated || !user?.id) {
      setData(null);
      setLoading(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    void (async () => {
      try {
        const dto = await postEnsurePosCashRegisterReady();
        if (!cancelled) {
          setData(dto);
        }
      } catch (e) {
        if (!cancelled) {
          setError(e instanceof Error ? e : new Error(String(e)));
          setData(null);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, user?.id, token]);

  const value = useMemo(
    () => ({
      data,
      loading,
      error,
      refresh,
    }),
    [data, loading, error, refresh]
  );

  return (
    <PosRegisterReadinessContext.Provider value={value}>{children}</PosRegisterReadinessContext.Provider>
  );
}
