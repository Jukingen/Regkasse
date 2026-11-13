import { useCallback, useEffect, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import {
  getMonatsbelegStatus,
  type MissingMonthDto,
  type MonatsbelegStatusDto,
} from '../services/api/rksvSpecialReceiptsService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';

const POLL_MS = 60_000;

export type MonatsbelegStatusHookResult = {
  /** True when current Vienna month is past grace or warningLevel is red. */
  isOverdue: boolean;
  /** Yellow or red RKSV reminder (or currentMonthOverdue). */
  requiresAttention: boolean;
  warningLevel: string | null;
  missingMonths: MissingMonthDto[];
  data: MonatsbelegStatusDto | null;
  loading: boolean;
  refresh: () => Promise<void>;
};

/**
 * POS Monatsbeleg status for the effective cash register (header badge / RKSV hints).
 * Polls every 60s; no react-query in the POS package.
 */
export function useMonatsbelegStatus(): MonatsbelegStatusHookResult {
  const posReadiness = usePosRegisterReadiness();
  const registerId = posReadiness.data?.effectiveRegisterId?.trim() ?? null;

  const [data, setData] = useState<MonatsbelegStatusDto | null>(null);
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!registerId || !isValidPosCashRegisterId(registerId)) {
      setData(null);
      return;
    }
    setLoading(true);
    try {
      const next = await getMonatsbelegStatus(registerId);
      setData(next);
    } catch {
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [registerId]);

  useEffect(() => {
    void refresh();
    if (!registerId || !isValidPosCashRegisterId(registerId)) {
      return;
    }
    const interval = setInterval(() => {
      void refresh();
    }, POLL_MS);
    return () => {
      clearInterval(interval);
    };
  }, [registerId, refresh]);

  const warningLevel = data?.warningLevel ?? null;
  const isOverdue = data?.currentMonthOverdue === true || warningLevel === 'red';
  const requiresAttention =
    isOverdue ||
    warningLevel === 'yellow' ||
    warningLevel === 'red' ||
    data?.requiresAttention === true;

  return {
    isOverdue,
    requiresAttention,
    warningLevel,
    missingMonths: data?.missingMonths ?? [],
    data,
    loading,
    refresh,
  };
}
