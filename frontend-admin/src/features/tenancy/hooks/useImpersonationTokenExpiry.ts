'use client';

import { useEffect, useState } from 'react';

import { authStorage } from '@/features/auth/services/authStorage';
import { decodeJwtPayload } from '@/lib/auth/jwtPayload';

/** Warn when impersonation JWT expires within this many minutes (exclusive: 5 min left still OK). */
export const IMPERSONATION_EXPIRY_WARN_MINUTES = 5;

export type ImpersonationTokenExpiryState = {
  expiresAtMs: number | null;
  minutesRemaining: number | null;
  shouldWarn: boolean;
};

/** Exported for unit tests (JWT `exp` → minutes remaining, warn when under 5 min). */
export function computeImpersonationTokenExpiryState(
  token: string | null
): ImpersonationTokenExpiryState {
  if (!token) {
    return { expiresAtMs: null, minutesRemaining: null, shouldWarn: false };
  }

  const payload = decodeJwtPayload(token);
  const exp = payload?.exp;
  if (typeof exp !== 'number' || !Number.isFinite(exp)) {
    return { expiresAtMs: null, minutesRemaining: null, shouldWarn: false };
  }

  const expiresAtMs = exp * 1000;
  const msRemaining = expiresAtMs - Date.now();
  const minutesRemaining = Math.max(0, Math.floor(msRemaining / 60_000));
  const shouldWarn = msRemaining > 0 && minutesRemaining < IMPERSONATION_EXPIRY_WARN_MINUTES;

  return { expiresAtMs, minutesRemaining, shouldWarn };
}

/**
 * Client-side JWT `exp` countdown for impersonation sessions (no signature verification).
 */
export function useImpersonationTokenExpiry(enabled: boolean): ImpersonationTokenExpiryState {
  const [state, setState] = useState<ImpersonationTokenExpiryState>(() =>
    enabled
      ? computeImpersonationTokenExpiryState(authStorage.getToken())
      : { expiresAtMs: null, minutesRemaining: null, shouldWarn: false }
  );

  useEffect(() => {
    if (!enabled) {
      setState({ expiresAtMs: null, minutesRemaining: null, shouldWarn: false });
      return;
    }

    const tick = () => setState(computeImpersonationTokenExpiryState(authStorage.getToken()));
    tick();
    const id = window.setInterval(tick, 30_000);
    return () => window.clearInterval(id);
  }, [enabled]);

  return state;
}
