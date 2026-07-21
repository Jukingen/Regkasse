import { useEffect, useState } from 'react';

import { useCompanySettings } from './useCompanySettings';
import {
  computeSmartTagesabschlussReminder,
  computeWorkingHoursClosingAt,
  formatCountdown,
  resolveReminderHoursBeforeClose,
  resolveTodayWorkingHoursDay,
} from '../utils/viennaTagesabschlussReminder';

const TICK_MS = 1_000;

export type UseTagesabschlussReminderResult = {
  /** Fractional hours until restaurant closing (0 when closed/unknown/overdue). */
  timeUntilClose: number;
  /** True when within the reminder window before closing. */
  shouldShowReminder: boolean;
  /** HH:MM:SS countdown for the banner. */
  countdownLabel: string;
  /** Whole hours remaining (ceil), for German copy. */
  hoursRemaining: number;
  /** Closing time label HH:mm when known. */
  closingTimeLabel: string | null;
  /** True when working-hours logic drove the reminder (not Vienna midnight fallback). */
  usedWorkingHours: boolean;
  loading: boolean;
};

/**
 * POS smart Tagesabschluss reminder based on restaurant working hours.
 * Shows within X hours of closing (default 1). Never auto-closes (RKSV).
 *
 * @example
 * const { timeUntilClose, shouldShowReminder } = useTagesabschlussReminder();
 */
export function useTagesabschlussReminder(): UseTagesabschlussReminderResult {
  const { data: settings, loading } = useCompanySettings();
  const [timeUntilClose, setTimeUntilClose] = useState(0);
  const [shouldShowReminder, setShouldShowReminder] = useState(false);
  const [countdownLabel, setCountdownLabel] = useState('00:00:00');
  const [hoursRemaining, setHoursRemaining] = useState(0);
  const [closingTimeLabel, setClosingTimeLabel] = useState<string | null>(null);
  const [usedWorkingHours, setUsedWorkingHours] = useState(false);

  useEffect(() => {
    const tick = () => {
      const workingHours = settings?.workingHours ?? null;
      const timeZone = settings?.timeZone?.trim() || 'Europe/Vienna';
      const now = new Date();
      const todayHours = resolveTodayWorkingHoursDay(workingHours);

      // No hours configured yet → Vienna midnight fallback (legacy POS behavior).
      if (!workingHours) {
        const smart = computeSmartTagesabschlussReminder({
          canClose: true,
          now,
          timeZone,
          workingHours: null,
        });
        setTimeUntilClose(smart.secondsRemaining / 3600);
        setShouldShowReminder(smart.shouldShow);
        setCountdownLabel(smart.countdownLabel);
        setHoursRemaining(smart.hoursRemaining);
        setClosingTimeLabel(smart.closingTimeLabel);
        setUsedWorkingHours(false);
        return;
      }

      if (!todayHours || todayHours.isClosed) {
        setTimeUntilClose(0);
        setShouldShowReminder(false);
        setCountdownLabel(formatCountdown(0));
        setHoursRemaining(0);
        setClosingTimeLabel(null);
        setUsedWorkingHours(false);
        return;
      }

      const reminderThreshold = resolveReminderHoursBeforeClose({
        workingHours,
        reminderHoursBeforeClose: (settings as { reminderHoursBeforeClose?: number } | null)
          ?.reminderHoursBeforeClose,
      });

      const closeTime = computeWorkingHoursClosingAt(now, timeZone, {
        ...workingHours,
        reminderHoursBeforeClosing: reminderThreshold,
      });

      if (!closeTime) {
        setTimeUntilClose(0);
        setShouldShowReminder(false);
        setCountdownLabel(formatCountdown(0));
        setHoursRemaining(0);
        setClosingTimeLabel(null);
        setUsedWorkingHours(false);
        return;
      }

      const hoursUntilClose = (closeTime.getTime() - now.getTime()) / (1000 * 60 * 60);

      setTimeUntilClose(Math.max(0, hoursUntilClose));
      // Sketch: show when within threshold and still before closing.
      setShouldShowReminder(hoursUntilClose <= reminderThreshold && hoursUntilClose > 0);

      const smart = computeSmartTagesabschlussReminder({
        canClose: true,
        now,
        timeZone,
        workingHours: {
          ...workingHours,
          reminderHoursBeforeClosing: reminderThreshold,
        },
      });
      setCountdownLabel(smart.countdownLabel);
      setHoursRemaining(smart.hoursRemaining);
      setClosingTimeLabel(smart.closingTimeLabel);
      setUsedWorkingHours(smart.usedWorkingHours);
    };

    tick();
    const interval = setInterval(tick, TICK_MS);
    return () => {
      clearInterval(interval);
    };
  }, [settings]);

  return {
    timeUntilClose,
    shouldShowReminder,
    countdownLabel,
    hoursRemaining,
    closingTimeLabel,
    usedWorkingHours,
    loading,
  };
}
