import { useCallback, useEffect, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { canPerformClosing } from '../services/api/tagesabschlussService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';
import {
  computePosTagesabschlussClosingRequired,
  computeViennaHoursRemainingUntilMidnight,
} from '../utils/viennaTagesabschlussReminder';

const STATUS_POLL_MS = 60_000;
const HOURS_TICK_MS = 60_000;

export type PosTagesabschlussStatus = {
  isClosingRequired: boolean;
  hoursRemaining: number;
  canClose: boolean;
  loading: boolean;
  refresh: () => Promise<void>;
};

/**
 * POS Tagesabschluss reminder status for the effective cash register.
 * Polls can-close; never auto-closes (RKSV: user must count cash and close manually).
 */
export function useTagesabschlussStatus(): PosTagesabschlussStatus {
  const posReadiness = usePosRegisterReadiness();
  const registerId = posReadiness.data?.effectiveRegisterId?.trim() ?? null;

  const [canClose, setCanClose] = useState(false);
  const [loading, setLoading] = useState(false);
  const [hoursRemaining, setHoursRemaining] = useState(() =>
    computeViennaHoursRemainingUntilMidnight(),
  );

  const refresh = useCallback(async () => {
    if (!registerId || !isValidPosCashRegisterId(registerId)) {
      setCanClose(false);
      return;
    }
    setLoading(true);
    try {
      const status = await canPerformClosing(registerId);
      setCanClose(status.canClose === true);
    } catch {
      setCanClose(false);
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
    }, STATUS_POLL_MS);
    return () => clearInterval(interval);
  }, [registerId, refresh]);

  useEffect(() => {
    setHoursRemaining(computeViennaHoursRemainingUntilMidnight());
    const interval = setInterval(() => {
      setHoursRemaining(computeViennaHoursRemainingUntilMidnight());
    }, HOURS_TICK_MS);
    return () => clearInterval(interval);
  }, []);

  const isClosingRequired = computePosTagesabschlussClosingRequired({ canClose });

  return {
    isClosingRequired,
    hoursRemaining,
    canClose,
    loading,
    refresh,
  };
}
