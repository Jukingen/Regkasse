import { describe, expect, it, vi } from 'vitest';

import {
    computeImpersonationTokenExpiryState,
    IMPERSONATION_EXPIRY_WARN_MINUTES,
} from '@/features/tenancy/hooks/useImpersonationTokenExpiry';

function tokenWithExp(expUnixSeconds: number): string {
    const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ exp: expUnixSeconds, tenant_impersonation: true }));
    return `${header}.${payload}.sig`;
}

describe('computeImpersonationTokenExpiryState', () => {
    it('warns when fewer than 5 minutes remain', () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-05-21T12:00:00Z'));

        const exp = Math.floor(Date.now() / 1000) + 4 * 60 + 30;
        const state = computeImpersonationTokenExpiryState(tokenWithExp(exp));

        expect(state.minutesRemaining).toBe(4);
        expect(state.shouldWarn).toBe(true);
        expect(IMPERSONATION_EXPIRY_WARN_MINUTES).toBe(5);

        vi.useRealTimers();
    });

    it('does not warn when 5 or more minutes remain', () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-05-21T12:00:00Z'));

        const exp = Math.floor(Date.now() / 1000) + 5 * 60;
        const state = computeImpersonationTokenExpiryState(tokenWithExp(exp));

        expect(state.minutesRemaining).toBe(5);
        expect(state.shouldWarn).toBe(false);

        vi.useRealTimers();
    });
});
