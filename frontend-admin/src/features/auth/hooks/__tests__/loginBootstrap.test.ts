import { QueryClient } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { authStorage } from '@/features/auth/services/authStorage';

import {
  AUTH_KEYS,
  POST_LOGIN_TOKEN_SETTLE_MS,
  clearStaleAuthBeforeLogin,
  persistLoginTokensAndSettle,
} from '../useAuth';

vi.mock('@/features/auth/services/authStorage', () => ({
  authStorage: {
    removeToken: vi.fn(),
    setToken: vi.fn(),
    setRefreshToken: vi.fn(),
  },
}));

describe('login bootstrap helpers', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('clearStaleAuthBeforeLogin removes tokens and /me cache', () => {
    const queryClient = new QueryClient();
    const removeQueries = vi.spyOn(queryClient, 'removeQueries');

    clearStaleAuthBeforeLogin(queryClient);

    expect(authStorage.removeToken).toHaveBeenCalledOnce();
    expect(removeQueries).toHaveBeenCalledWith({ queryKey: AUTH_KEYS.user });
  });

  it('persistLoginTokensAndSettle stores tokens then waits before resolving', async () => {
    const promise = persistLoginTokensAndSettle('access-abc', 'refresh-xyz');

    expect(authStorage.setToken).toHaveBeenCalledWith('access-abc');
    expect(authStorage.setRefreshToken).toHaveBeenCalledWith('refresh-xyz');

    vi.advanceTimersByTime(POST_LOGIN_TOKEN_SETTLE_MS - 1);
    let settled = false;
    void promise.then(() => {
      settled = true;
    });
    await Promise.resolve();
    expect(settled).toBe(false);

    vi.advanceTimersByTime(1);
    await promise;
    expect(settled).toBe(true);
  });
});
