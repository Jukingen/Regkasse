import { useCallback, useEffect, useState } from 'react';

import { usePosRegisterReadiness } from '../contexts/PosRegisterReadinessContext';
import { useTagesabschlussReminder } from './useTagesabschlussReminder';
import { canPerformClosing } from '../services/api/tagesabschlussService';
import { isValidPosCashRegisterId } from '../utils/posCashRegister';

const STATUS_POLL_MS = 60_000;

export type PosTagesabschlussStatus = {
  /**
   * True when a closing is allowed and the reminder window is active.
   * Informational for UI emphasis only — never blocks POS sales.
   */
  isClosingRequired: boolean;
  /** True when within the working-hours / midnight reminder window. */
  shouldShowReminder: boolean;
  hoursRemaining: number;
  secondsRemaining: number;
  countdownLabel: string;
  closingTimeLabel: string | null;
  usedWorkingHours: boolean;
  timeUntilClose: number;
  /** Backend: Tagesabschluss is allowed for this register (not a sales lock). */
  canClose: boolean;
  loading: boolean;
  refresh: () => Promise<void>;
};

/**
 * POS Tagesabschluss reminder status for the effective cash register.
 * Combines working-hours smart reminder with can-close.
 * Never auto-closes and never blocks orders/payments (RKSV).
 */
export function useTagesabschlussStatus(): PosTagesabschlussStatus {
  const posReadiness = usePosRegisterReadiness();
  const registerId = posReadiness.data?.effectiveRegisterId?.trim() ?? null;
  const reminder = useTagesabschlussReminder();

  const [canClose, setCanClose] = useState(false);
  const [statusLoading, setStatusLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!registerId || !isValidPosCashRegisterId(registerId)) {
      setCanClose(false);
      return;
    }
    setStatusLoading(true);
    try {
      const status = await canPerformClosing(registerId);
      setCanClose(status.canClose === true);
    } catch {
      setCanClose(false);
    } finally {
      setStatusLoading(false);
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

  const shouldShowReminder = reminder.shouldShowReminder;
  const isClosingRequired = canClose && shouldShowReminder;
  const secondsRemaining = Math.max(0, Math.floor(reminder.timeUntilClose * 3600));

  return {
    isClosingRequired,
    shouldShowReminder,
    hoursRemaining: reminder.hoursRemaining,
    secondsRemaining,
    countdownLabel: reminder.countdownLabel,
    closingTimeLabel: reminder.closingTimeLabel,
    usedWorkingHours: reminder.usedWorkingHours,
    timeUntilClose: reminder.timeUntilClose,
    canClose,
    loading: reminder.loading || statusLoading,
    refresh,
  };
}
