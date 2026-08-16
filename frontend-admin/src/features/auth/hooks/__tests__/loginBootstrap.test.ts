import { QueryClient } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { authStorage } from '@/features/auth/services/authStorage';
import { customInstance } from '@/lib/axios';

import {
  AUTH_KEYS,
  POST_LOGIN_TOKEN_SETTLE_MS,
  clearStaleAuthBeforeLogin,
  fetchAuthUserWithRetry,
  persistLoginTokensAndSettle,
} from '../useAuth';

vi.mock('@/features/auth/services/authStorage', () => ({
  authStorage: {
    removeToken: vi.fn(),
    setToken: vi.fn(),
    setRefreshToken: vi.fn(),
    setTokens: vi.fn(),
    getToken: vi.fn(() => 'access-abc'),
  },
}));

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(),
}));

describe('login bootstrap helpers', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
    vi.mocked(authStorage.getToken).mockReturnValue('access-abc');
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

    expect(authStorage.setTokens).toHaveBeenCalledWith({
      accessToken: 'access-abc',
      refreshToken: 'refresh-xyz',
    });

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

  it('fetchAuthUserWithRetry retries /me once after 401 then succeeds', async () => {
    const meUser = {
      id: 'u1',
      email: 'a@b.c',
      userName: 'admin',
      firstName: 'A',
      lastName: 'B',
      roles: ['Manager'],
      permissions: [],
      tenantId: null,
      tenantSlug: null,
      mustChangePasswordOnNextLogin: false,
    };

    vi.mocked(customInstance)
      .mockRejectedValueOnce({ response: { status: 401 } })
      .mockResolvedValueOnce(meUser);

    const promise = fetchAuthUserWithRetry(3, 100);
    await vi.advanceTimersByTimeAsync(100);
    const user = await promise;

    expect(customInstance).toHaveBeenCalledTimes(2);
    expect(user.id).toBe('u1');
  });

  it('fetchAuthUserWithRetry does not retry when token was cleared', async () => {
    vi.mocked(authStorage.getToken).mockReturnValue(null);
    vi.mocked(customInstance).mockRejectedValue({ response: { status: 401 } });

    await expect(fetchAuthUserWithRetry(3, 100)).rejects.toMatchObject({
      response: { status: 401 },
    });
    expect(customInstance).toHaveBeenCalledTimes(1);
  });

  it('fetchAuthUserWithRetry ignores React Query context passed as first arg', async () => {
    const meUser = {
      id: 'u1',
      email: 'a@b.c',
      userName: 'admin',
      firstName: 'A',
      lastName: 'B',
      roles: ['Manager'],
      permissions: [],
      tenantId: null,
      tenantSlug: null,
      mustChangePasswordOnNextLogin: false,
    };
    vi.mocked(customInstance).mockResolvedValueOnce(meUser);

    // Simulates accidental `queryFn: fetchAuthUserWithRetry` (RQ context as arg0).
    const user = await fetchAuthUserWithRetry({ queryKey: AUTH_KEYS.user } as never);

    expect(customInstance).toHaveBeenCalledTimes(1);
    expect(user.id).toBe('u1');
  });
});
