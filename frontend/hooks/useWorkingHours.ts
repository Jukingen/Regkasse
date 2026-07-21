import { useEffect, useState } from 'react';

import { useCompanySettings } from './useCompanySettings';
import {
  computePosWorkingHoursStatus,
  type PosWorkingHoursExtended,
  type PosWorkingHoursStatus,
} from '../utils/workingHoursStatus';

const TICK_MS = 60_000;

export type UseWorkingHoursResult = PosWorkingHoursStatus & {
  loading: boolean;
};

const INITIAL: PosWorkingHoursStatus = {
  posOperationsAllowed: true,
  restaurantIsOpen: false,
  isOpen: false,
  isClosingSoon: false,
  showReminder: false,
  timeUntilClose: 0,
  timeUntilOpen: 0,
  isSpecialDay: false,
  message: 'Heute geschlossen',
  preferClosingPrompt: false,
  stopOnlineOrdersMinutesBeforeClose: 30,
  openTime: null,
  closeTime: null,
};

/**
 * POS working-hours status for **display and Tagesabschluss reminders only**.
 *
 * CRITICAL (AGENTS / RKSV):
 * - {@link UseWorkingHoursResult.posOperationsAllowed} is **always** `true`.
 * - Never use this hook to block cart, orders, payments, or register access.
 * - Online-order cutoffs are enforced on Web/App + public API — not here.
 *
 * @example
 * const { restaurantIsOpen, showReminder, timeUntilClose, message } = useWorkingHours();
 */
export function useWorkingHours(): UseWorkingHoursResult {
  const { data: settings, loading } = useCompanySettings();
  const [status, setStatus] = useState<PosWorkingHoursStatus>(INITIAL);

  useEffect(() => {
    const tick = () => {
      const workingHours = settings?.workingHours ?? null;
      const next = computePosWorkingHoursStatus({
        now: new Date(),
        timeZone: settings?.timeZone,
        workingHours,
      });
      // Harden: POS operations must never be gated even if compute regresses.
      setStatus({ ...next, posOperationsAllowed: true });
    };

    tick();
    const interval = setInterval(tick, TICK_MS);
    return () => {
      clearInterval(interval);
    };
  }, [settings]);

  return { ...status, posOperationsAllowed: true, loading };
}
