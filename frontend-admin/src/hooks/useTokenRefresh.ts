'use client';

import { useEffect, useRef } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { authStorage } from '@/features/auth/services/authStorage';
import { decodeJwtPayload } from '@/lib/auth/jwtPayload';
import { technicalConsole } from '@/shared/dev/technicalConsole';

/** Proactive refresh window before JWT <c>exp</c> (access token default: 24h). */
export const TOKEN_REFRESH_BEFORE_EXPIRY_MS = 5 * 60 * 1000;

/**
 * Delay until proactive refresh should run for a JWT access token.
 * Returns `null` when the token cannot be scheduled (missing / invalid `exp`).
 * Returns `0` when the token is already within the refresh window (refresh immediately).
 */
export function computeTokenRefreshDelayMs(
  token: string | null,
  nowMs: number = Date.now()
): number | null {
  if (!token) {
    return null;
  }

  const payload = decodeJwtPayload(token);
  const exp = payload?.exp;
  if (typeof exp !== 'number' || !Number.isFinite(exp)) {
    return null;
  }

  const expiresAtMs = exp * 1000;
  const refreshAtMs = expiresAtMs - TOKEN_REFRESH_BEFORE_EXPIRY_MS;
  return Math.max(0, refreshAtMs - nowMs);
}

/**
 * Silently rotates the access token ~5 minutes before JWT `exp`.
 * Mount only in authenticated shells (e.g. `AppLayout`); no user interaction.
 */
export function useTokenRefresh(enabled = true): void {
  const { refreshToken, isAuthenticated } = useAuth();
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const inFlightRef = useRef(false);

  useEffect(() => {
    if (!enabled || !isAuthenticated) {
      return;
    }

    const clearTimer = () => {
      if (timerRef.current !== undefined) {
        clearTimeout(timerRef.current);
        timerRef.current = undefined;
      }
    };

    const schedule = () => {
      clearTimer();

      const delay = computeTokenRefreshDelayMs(authStorage.getToken());
      if (delay === null) {
        return;
      }

      const runRefresh = async () => {
        if (inFlightRef.current) {
          return;
        }
        inFlightRef.current = true;
        try {
          const ok = await refreshToken();
          if (ok) {
            schedule();
          }
        } catch (error) {
          technicalConsole.error('Proactive token refresh failed', error);
        } finally {
          inFlightRef.current = false;
        }
      };

      if (delay === 0) {
        void runRefresh();
        return;
      }

      timerRef.current = setTimeout(() => {
        void runRefresh();
      }, delay);
    };

    schedule();

    return clearTimer;
  }, [enabled, isAuthenticated, refreshToken]);
}
