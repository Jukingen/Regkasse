'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';

const ACTIVITY_EVENTS = [
  'mousemove',
  'mousedown',
  'keydown',
  'scroll',
  'wheel',
  'click',
  'touchstart',
] as const;

export type SessionTimeoutOptions = {
  /** Total idle time before logout (default 30). */
  timeoutMinutes?: number;
  /** Warning window length before logout (default 5). */
  warningMinutes?: number;
  onTimeout?: () => void;
  enabled?: boolean;
};

export type UseSessionTimeoutResult = {
  showWarning: boolean;
  secondsRemaining: number;
  resetTimers: () => void;
};

/**
 * Idle session timer with warning countdown and auto-logout.
 * Resets on keyboard / mouse / touch / scroll activity.
 */
export function useSessionTimeout(options: SessionTimeoutOptions = {}): UseSessionTimeoutResult {
  const { timeoutMinutes = 30, warningMinutes = 5, onTimeout, enabled = true } = options;
  const { logout } = useAuth();

  const [showWarning, setShowWarning] = useState(false);
  const [secondsRemaining, setSecondsRemaining] = useState(() => Math.max(1, warningMinutes * 60));

  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const warningRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const logoutRef = useRef(logout);
  const onTimeoutRef = useRef(onTimeout);

  useEffect(() => {
    logoutRef.current = logout;
    onTimeoutRef.current = onTimeout;
  }, [logout, onTimeout]);

  const clearTimers = useCallback(() => {
    if (timeoutRef.current) {
      clearTimeout(timeoutRef.current);
      timeoutRef.current = null;
    }
    if (warningRef.current) {
      clearTimeout(warningRef.current);
      warningRef.current = null;
    }
    if (intervalRef.current) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const resetTimers = useCallback(() => {
    if (!enabled || timeoutMinutes <= 0) return;

    clearTimers();
    setShowWarning(false);
    setSecondsRemaining(Math.max(1, warningMinutes * 60));

    const warningDelayMs = Math.max(0, (timeoutMinutes - warningMinutes) * 60 * 1000);
    const warningDurationMs = Math.max(1, warningMinutes * 60 * 1000);

    warningRef.current = setTimeout(() => {
      setShowWarning(true);
      setSecondsRemaining(Math.max(1, warningMinutes * 60));

      intervalRef.current = setInterval(() => {
        setSecondsRemaining((prev) => (prev <= 1 ? 0 : prev - 1));
      }, 1000);

      timeoutRef.current = setTimeout(() => {
        clearTimers();
        setShowWarning(false);
        void logoutRef.current();
        onTimeoutRef.current?.();
      }, warningDurationMs);
    }, warningDelayMs);
  }, [clearTimers, enabled, timeoutMinutes, warningMinutes]);

  useEffect(() => {
    if (!enabled) {
      clearTimers();
      setShowWarning(false);
      return;
    }

    const handleActivity = () => resetTimers();

    ACTIVITY_EVENTS.forEach((event) => {
      window.addEventListener(event, handleActivity, { passive: true });
    });

    resetTimers();

    return () => {
      ACTIVITY_EVENTS.forEach((event) => {
        window.removeEventListener(event, handleActivity);
      });
      clearTimers();
    };
  }, [clearTimers, enabled, resetTimers]);

  return { showWarning, secondsRemaining, resetTimers };
}
