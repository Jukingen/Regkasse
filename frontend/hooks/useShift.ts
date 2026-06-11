import { useCallback, useEffect, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import {
  DailyClosingApiError,
  endShiftApi,
  fetchCurrentShift,
  fetchDailyClosingStatus,
  performDailyClosingApi,
  startShiftApi,
  type CashierShiftDto,
  type EndShiftResponse,
  type PosDailyClosingResult,
  type PosDailyClosingStatusDto,
} from '../services/api/shiftService';
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
  const cashRegisterId =
    (explicitRegisterId?.trim() && isValidPosCashRegisterId(explicitRegisterId.trim())
      ? explicitRegisterId.trim()
      : null) ??
    (isValidPosCashRegisterId(posReadiness.data?.effectiveRegisterId)
      ? posReadiness.data!.effectiveRegisterId!.trim()
      : null);

  const [activeShift, setActiveShift] = useState<CashierShiftDto | null>(null);
  const [dailyClosingStatus, setDailyClosingStatus] = useState<PosDailyClosingStatusDto | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    try {
      const res = await fetchCurrentShift();
      setActiveShift(res.hasActiveShift && res.shift ? res.shift : null);
      setError(null);
    } catch (e) {
      setError(readApiErrorMessage(e));
    } finally {
      setIsLoading(false);
    }
  }, []);

  const refreshDailyClosingStatus = useCallback(async () => {
    try {
      const status = await fetchDailyClosingStatus();
      setDailyClosingStatus(status);
    } catch {
      setDailyClosingStatus(null);
    }
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh, cashRegisterId]);

  useEffect(() => {
    if (!activeShift) {
      setDailyClosingStatus(null);
      return;
    }
    void refreshDailyClosingStatus();
  }, [activeShift?.id, refreshDailyClosingStatus]);

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
        await posReadiness.refreshAsync();
        return shift;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        throw e;
      } finally {
        setIsLoading(false);
      }
    },
    [cashRegisterId, posReadiness]
  );

  const endShift = useCallback(
    async (endBalance: number, notes?: string): Promise<EndShiftResponse> => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await endShiftApi(endBalance, notes);
        setActiveShift(null);
        await posReadiness.refreshAsync();
        return result;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        throw e;
      } finally {
        setIsLoading(false);
      }
    },
    [posReadiness]
  );

  const performDailyClosing = useCallback(
    async (cashCount: number, notes?: string): Promise<PosDailyClosingResult> => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await performDailyClosingApi(cashCount, notes);
        setActiveShift(null);
        setDailyClosingStatus(null);
        await posReadiness.refreshAsync();
        await refresh();
        return result;
      } catch (e) {
        const msg =
          e instanceof DailyClosingApiError
            ? e.message
            : readApiErrorMessage(e);
        setError(msg);
        if (e instanceof DailyClosingApiError) throw e;
        throw new Error(msg);
      } finally {
        setIsLoading(false);
      }
    },
    [posReadiness, refresh]
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
  };
}
