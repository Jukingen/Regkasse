'use client';

import { useCallback, useEffect, useRef } from 'react';

export type IdleTimeoutConfig = {
  timeoutMinutes: number;
  warningBeforeMinutes: number;
  onWarning: () => void;
  onTimeout: () => void;
  enabled?: boolean;
};

export type UseIdleTimeoutResult = {
  /** Resets idle + warning timers (e.g. "Continue session"). */
  reset: () => void;
};

/**
 * @deprecated Prefer `useSessionTimeout` for warning countdown + auto-logout.
 * Web admin idle detection: mouse, keyboard, touch, scroll.
 */
export function useIdleTimeout({
  timeoutMinutes,
  warningBeforeMinutes,
  onWarning,
  onTimeout,
  enabled = true,
}: IdleTimeoutConfig): UseIdleTimeoutResult {
  const onWarningRef = useRef(onWarning);
  const onTimeoutRef = useRef(onTimeout);
  const warningShownRef = useRef(false);
  const scheduleRef = useRef<() => void>(() => {});

  useEffect(() => {
    onWarningRef.current = onWarning;
    onTimeoutRef.current = onTimeout;
  }, [onWarning, onTimeout]);

  useEffect(() => {
    if (!enabled || timeoutMinutes <= 0) return;

    const timeoutMs = timeoutMinutes * 60 * 1000;
    const warningMs = Math.max(0, (timeoutMinutes - Math.max(0, warningBeforeMinutes)) * 60 * 1000);

    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    let warningId: ReturnType<typeof setTimeout> | undefined;

    const clearAll = () => {
      if (timeoutId) clearTimeout(timeoutId);
      if (warningId) clearTimeout(warningId);
      timeoutId = undefined;
      warningId = undefined;
    };

    const schedule = () => {
      clearAll();
      warningShownRef.current = false;

      if (warningBeforeMinutes > 0 && warningMs < timeoutMs) {
        warningId = setTimeout(() => {
          if (!warningShownRef.current) {
            warningShownRef.current = true;
            onWarningRef.current();
          }
        }, warningMs);
      }

      timeoutId = setTimeout(() => {
        onTimeoutRef.current();
      }, timeoutMs);
    };

    scheduleRef.current = schedule;

    const onActivity = () => schedule();

    const events: (keyof WindowEventMap)[] = [
      'mousemove',
      'mousedown',
      'keydown',
      'touchstart',
      'scroll',
      'wheel',
    ];

    events.forEach((ev) => window.addEventListener(ev, onActivity, { passive: true }));
    schedule();

    return () => {
      clearAll();
      events.forEach((ev) => window.removeEventListener(ev, onActivity));
    };
  }, [enabled, timeoutMinutes, warningBeforeMinutes]);

  const reset = useCallback(() => {
    scheduleRef.current();
  }, []);

  return { reset };
}
