import { describe, expect, it, vi } from 'vitest';

import {
  IMPERSONATION_EXPIRY_WARN_MINUTES,
  computeImpersonationTokenExpiryState,
} from '@/features/tenancy/hooks/useImpersonationTokenExpiry';

describe('computeImpersonationTokenExpiryState', () => {
  it('warns when fewer than 5 minutes remain', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-21T12:00:00Z'));

    const exp = Math.floor(Date.now() / 1000) + 4 * 60 + 30;
    const state = computeImpersonationTokenExpiryState(exp * 1000);

    expect(state.minutesRemaining).toBe(4);
    expect(state.shouldWarn).toBe(true);
    expect(IMPERSONATION_EXPIRY_WARN_MINUTES).toBe(5);

    vi.useRealTimers();
  });

  it('does not warn when 5 or more minutes remain', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-21T12:00:00Z'));

    const exp = Math.floor(Date.now() / 1000) + 5 * 60;
    const state = computeImpersonationTokenExpiryState(exp * 1000);

    expect(state.minutesRemaining).toBe(5);
    expect(state.shouldWarn).toBe(false);

    vi.useRealTimers();
  });
});
