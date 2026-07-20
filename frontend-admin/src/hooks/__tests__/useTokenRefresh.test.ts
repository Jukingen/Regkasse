import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import {
    TOKEN_REFRESH_BEFORE_EXPIRY_MS,
    computeTokenRefreshDelayMs,
    useTokenRefresh,
} from '@/hooks/useTokenRefresh';

const mockRefreshToken = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
    useAuth: () => ({
        refreshToken: mockRefreshToken,
        isAuthenticated: true,
    }),
}));

vi.mock('@/features/auth/services/authStorage', () => ({
    authStorage: {
        getToken: vi.fn(),
    },
}));

import { authStorage } from '@/features/auth/services/authStorage';

function tokenWithExp(expUnixSeconds: number): string {
    const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
    const payload = btoa(JSON.stringify({ exp: expUnixSeconds }));
    return `${header}.${payload}.sig`;
}

describe('computeTokenRefreshDelayMs', () => {
    it('schedules refresh 5 minutes before access-token expiry (24h default)', () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-07-20T12:00:00Z'));

        const now = Date.now();
        const exp = Math.floor(now / 1000) + 24 * 60 * 60;
        const delay = computeTokenRefreshDelayMs(tokenWithExp(exp), now);

        expect(TOKEN_REFRESH_BEFORE_EXPIRY_MS).toBe(5 * 60 * 1000);
        expect(delay).toBe(24 * 60 * 60 * 1000 - TOKEN_REFRESH_BEFORE_EXPIRY_MS);

        vi.useRealTimers();
    });

    it('returns 0 when already inside the refresh window', () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-07-20T12:00:00Z'));

        const now = Date.now();
        const exp = Math.floor(now / 1000) + 3 * 60;
        expect(computeTokenRefreshDelayMs(tokenWithExp(exp), now)).toBe(0);

        vi.useRealTimers();
    });

    it('returns null for missing or invalid tokens', () => {
        expect(computeTokenRefreshDelayMs(null)).toBeNull();
        expect(computeTokenRefreshDelayMs('not-a-jwt')).toBeNull();
    });
});

describe('useTokenRefresh', () => {
    beforeEach(() => {
        mockRefreshToken.mockReset();
        mockRefreshToken.mockImplementation(async () => {
            // Simulate rotated access token with a fresh 24h lifetime.
            vi.mocked(authStorage.getToken).mockReturnValue(
                tokenWithExp(Math.floor(Date.now() / 1000) + 24 * 60 * 60),
            );
            return true;
        });
        vi.mocked(authStorage.getToken).mockReset();
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-07-20T12:00:00Z'));
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('refreshes immediately when token is near expiry', async () => {
        const now = Date.now();
        vi.mocked(authStorage.getToken).mockReturnValue(
            tokenWithExp(Math.floor(now / 1000) + 2 * 60),
        );

        renderHook(() => useTokenRefresh(true));

        await act(async () => {
            await Promise.resolve();
        });

        expect(mockRefreshToken).toHaveBeenCalledTimes(1);
    });

    it('refreshes after the scheduled delay without user interaction', async () => {
        const now = Date.now();
        const expInMs = 10 * 60 * 1000;
        vi.mocked(authStorage.getToken).mockReturnValue(
            tokenWithExp(Math.floor((now + expInMs) / 1000)),
        );

        renderHook(() => useTokenRefresh(true));

        expect(mockRefreshToken).not.toHaveBeenCalled();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(expInMs - TOKEN_REFRESH_BEFORE_EXPIRY_MS - 1);
        });
        expect(mockRefreshToken).not.toHaveBeenCalled();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(1);
        });
        expect(mockRefreshToken).toHaveBeenCalledTimes(1);
    });

    it('does not schedule when disabled', () => {
        const now = Date.now();
        vi.mocked(authStorage.getToken).mockReturnValue(
            tokenWithExp(Math.floor(now / 1000) + 2 * 60),
        );

        renderHook(() => useTokenRefresh(false));

        expect(mockRefreshToken).not.toHaveBeenCalled();
    });
});
