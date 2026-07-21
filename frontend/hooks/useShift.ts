import { useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { usePosStatusOverview } from '../contexts/PosStatusOverviewContext';
import {
  DailyClosingApiError,
  endShiftApi,
  fetchCurrentShift,
  startShiftApi,
  type CashierShiftDto,
  type EndShiftResponse,
  type PosDailyClosingResult,
  type PosDailyClosingStatusDto,
} from '../services/api/shiftService';
import {
  getDailyClosingStatus,
  performDailyClosing as performDailyClosingRequest,
} from '../services/dailyClosingService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';

function readApiErrorMessage(error: unknown): string {
  const e = error as { response?: { data?: unknown }; data?: unknown; message?: string } | null;
  const data = (e?.response?.data ?? e?.data) as Record<string, unknown> | undefined;
  if (data && typeof data.error === 'string') return data.error;
  if (data && typeof data.message === 'string') return data.message;
  if (typeof e?.message === 'string' && e.message) return e.message;
  return 'Unbekannter Fehler';
}

export function useShift(explicitRegisterId?: string | null) {
  const posReadiness = usePosRegisterReadiness();
  const { refreshOverview } = usePosStatusOverview();
  const cashRegisterId =
    (explicitRegisterId?.trim() && isValidPosCashRegisterId(explicitRegisterId.trim())
      ? explicitRegisterId.trim()
      : null) ??
    (isValidPosCashRegisterId(posReadiness.data?.effectiveRegisterId)
      ? posReadiness.data!.effectiveRegisterId!.trim()
      : null);

  const [activeShift, setActiveShift] = useState<CashierShiftDto | null>(null);
  const [dailyClosingStatus, setDailyClosingStatus] = useState<PosDailyClosingStatusDto | null>(
    null
  );
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dailyClosingRefreshInFlightRef = useRef(false);
  const currentShiftRefreshInFlightRef = useRef(false);
  const lastShiftRefreshRegisterIdRef = useRef<string | undefined>(undefined);
  const activeShiftId = activeShift?.id ?? null;

  const refresh = useCallback(async () => {
    if (currentShiftRefreshInFlightRef.current) return;
    currentShiftRefreshInFlightRef.current = true;
    setIsLoading(true);
    try {
      const res = await fetchCurrentShift();
      setActiveShift(res.hasActiveShift && res.shift ? res.shift : null);
      setError(null);
      lastShiftRefreshRegisterIdRef.current = cashRegisterId ?? '__none__';
    } catch (e) {
      setError(readApiErrorMessage(e));
    } finally {
      setIsLoading(false);
      currentShiftRefreshInFlightRef.current = false;
    }
  }, [cashRegisterId]);

  const refreshDailyClosingStatus = useCallback(async () => {
    if (dailyClosingRefreshInFlightRef.current) return;
    dailyClosingRefreshInFlightRef.current = true;
    try {
      const [status, shiftRes] = await Promise.all([getDailyClosingStatus(), fetchCurrentShift()]);
      setDailyClosingStatus(status);
      if (shiftRes.hasActiveShift && shiftRes.shift) {
        setActiveShift(shiftRes.shift);
      }
    } catch {
      setDailyClosingStatus(null);
    } finally {
      dailyClosingRefreshInFlightRef.current = false;
    }
  }, []);

  const refreshAll = useCallback(async () => {
    await Promise.all([refresh(), refreshDailyClosingStatus()]);
  }, [refresh, refreshDailyClosingStatus]);

  useEffect(() => {
    const registerKey = cashRegisterId ?? '__none__';
    if (registerKey === lastShiftRefreshRegisterIdRef.current) return;
    void refresh();
  }, [refresh, cashRegisterId]);

  useEffect(() => {
    if (!activeShiftId) {
      setDailyClosingStatus(null);
      return;
    }
    void refreshDailyClosingStatus();
  }, [activeShiftId, cashRegisterId, refreshDailyClosingStatus]);

  useFocusEffect(
    useCallback(() => {
      if (!activeShiftId) return;
      void refreshDailyClosingStatus();
    }, [activeShiftId, refreshDailyClosingStatus])
  );

  const startShift = useCallback(
    async (startBalance: number): Promise<CashierShiftDto> => {
      if (!cashRegisterId) {
        throw new Error('NO_REGISTER');
      }
      setIsLoading(true);
      setError(null);
      try {
        const shift = await startShiftApi(cashRegisterId, startBalance);
        setActiveShift(shift);
        await refreshDailyClosingStatus();
        await posReadiness.refreshAsync();
        await refreshOverview(true);
        return shift;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        throw e;
      } finally {
        setIsLoading(false);
      }
    },
    [cashRegisterId, posReadiness, refreshOverview]
  );

  const endShift = useCallback(
    async (endBalance: number, notes?: string): Promise<EndShiftResponse> => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await endShiftApi(endBalance, notes);
        setActiveShift(null);
        await posReadiness.refreshAsync();
        await refreshOverview(true);
        return result;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        throw e;
      } finally {
        setIsLoading(false);
      }
    },
    [posReadiness, refreshOverview]
  );

  const performDailyClosing = useCallback(
    async (cashCount: number, notes?: string): Promise<PosDailyClosingResult> => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await performDailyClosingRequest({ cashCount, notes });
        setActiveShift(null);
        setDailyClosingStatus(null);
        await posReadiness.refreshAsync();
        await refreshOverview(true);
        await refresh();
        return result;
      } catch (e) {
        const msg = e instanceof DailyClosingApiError ? e.message : readApiErrorMessage(e);
        setError(msg);
        if (e instanceof DailyClosingApiError) throw e;
        throw new Error(msg);
      } finally {
        setIsLoading(false);
      }
    },
    [posReadiness, refreshOverview, refresh]
  );

  return {
    activeShift,
    cashRegisterId,
    dailyClosingStatus,
    startShift,
    endShift,
    performDailyClosing,
    isLoading,
    error,
    refresh,
    refreshDailyClosingStatus,
    refreshAll,
  };
}
