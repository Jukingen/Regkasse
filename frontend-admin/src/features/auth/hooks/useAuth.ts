'use client';

import { type QueryClient, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';

import { usePostApiAuthLogout } from '@/api/generated/auth/auth';
import { authStorage } from '@/features/auth/services/authStorage';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import {
  type MeResponse,
  mapMeResponseToAuthUser,
} from '@/features/auth/utils/mapMeResponseToAuthUser';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';
import { customInstance, refreshAccessToken } from '@/lib/axios';
import type { AuthUser } from '@/shared/auth/types';
import { technicalConsole } from '@/shared/dev/technicalConsole';

// Define the key for the user query
export const AUTH_KEYS = {
  user: ['auth', 'me'] as const,
};

/** Brief pause after marking the Edge session so Set-Cookie / cookie jar can settle. */
export const POST_LOGIN_TOKEN_SETTLE_MS = 100;

/** Clears stale client session metadata and in-flight `/me` before a fresh login attempt. */
export function clearStaleAuthBeforeLogin(queryClient: QueryClient): void {
  authStorage.removeToken();
  queryClient.removeQueries({ queryKey: AUTH_KEYS.user });
}

/**
 * Marks the FA Edge session (non-secret cookie). JWTs stay in HttpOnly API cookies.
 * Optional access token is decoded for expiry / impersonation metadata only.
 */
export async function persistLoginTokensAndSettle(
  accessToken?: string | null,
  refreshToken?: string | null,
  expiresAt?: string | Date | null
): Promise<void> {
  if (accessToken?.trim()) {
    const tokens: { accessToken: string; refreshToken?: string | null; expiresAt?: string | Date | null } =
      { accessToken, refreshToken };
    if (expiresAt) {
      tokens.expiresAt = expiresAt;
    }
    authStorage.setTokens(tokens);
  } else {
    authStorage.markSession(expiresAt);
  }
  await new Promise<void>((resolve) => {
    setTimeout(resolve, POST_LOGIN_TOKEN_SETTLE_MS);
  });
}

const emptySubscribe = () => () => {};

function getAuthHttpStatus(err: unknown): number | undefined {
  const e = err as { response?: { status?: number }; normalized?: { status?: number } } | undefined;
  return e?.response?.status ?? e?.normalized?.status;
}

function isAuthHttpError(err: unknown): boolean {
  const status = getAuthHttpStatus(err);
  return status === 401 || status === 403;
}

/** Shared with `LoginForm` so post-login bootstrap uses the same /me mapping as `useAuth`. */
export async function fetchAuthUser(): Promise<AuthUser> {
  if (process.env.NODE_ENV === 'development') {
    technicalConsole.devLog('[API] Fetching GET /api/Auth/me');
  }
  const res = await customInstance<MeResponse>({
    url: '/api/Auth/me',
    method: 'GET',
  });

  return mapMeResponseToAuthUser(res);
}

const POST_LOGIN_ME_RETRY_DELAY_MS = 300;

/**
 * Post-login `/me` bootstrap with short 401 retries.
 * Used when a concurrent stale request briefly races the fresh session token.
 *
 * Do not pass this function directly as a React Query `queryFn` — RQ supplies a
 * QueryFunctionContext as the first argument. Use `() => fetchAuthUserWithRetry()`.
 */
export async function fetchAuthUserWithRetry(
  retries = 3,
  delayMs = POST_LOGIN_ME_RETRY_DELAY_MS
): Promise<AuthUser> {
  const maxAttempts = typeof retries === 'number' && Number.isFinite(retries) ? retries : 3;
  const baseDelayMs =
    typeof delayMs === 'number' && Number.isFinite(delayMs) ? delayMs : POST_LOGIN_ME_RETRY_DELAY_MS;
  let attempt = 0;
  let lastError: unknown;

  while (attempt < maxAttempts) {
    attempt += 1;
    try {
      if (process.env.NODE_ENV === 'development') {
        technicalConsole.devDebug(`[API] /me bootstrap attempt ${attempt}/${maxAttempts}`);
      }
      return await fetchAuthUser();
    } catch (error) {
      lastError = error;
      const status = getAuthHttpStatus(error);
      const stillHasSession = Boolean(authStorage.hasToken());
      if (status !== 401 || !stillHasSession || attempt >= maxAttempts) {
        throw error;
      }
      await new Promise<void>((resolve) => {
        setTimeout(resolve, Math.round(baseDelayMs * 1.5 ** (attempt - 1)));
      });
    }
  }

  throw lastError ?? new Error('fetchAuthUserWithRetry failed without an error');
}

export enum AuthStatus {
  Loading = 'loading',
  Authenticated = 'authenticated',
  Unauthenticated = 'unauthenticated',
}

export const useAuth = () => {
  const { message } = useAntdApp();

  const { t } = useI18n();
  const queryClient = useQueryClient();
  const router = useRouter();

  const isBrowser = useSyncExternalStore(
    emptySubscribe,
    () => true,
    () => false
  );

  const {
    data: user,
    isError,
    error,
    refetch,
    isFetched,
    isFetching,
    fetchStatus,
  } = useQuery({
    queryKey: AUTH_KEYS.user,
    queryFn: fetchAuthUser,
    retry: false, // Strictly no retries for /me (refresh handled inside axios for 401)
    staleTime: 1000 * 30, // 30 seconds
    gcTime: 1000 * 60 * 10,
    refetchOnWindowFocus: false,
    refetchOnMount: false,
    // Cookie session: always probe /me in the browser. 401 → unauthenticated.
    enabled: isBrowser,
  });

  const effectiveUser = user;
  const querySettled = isFetched || isError || !isBrowser;

  const transientRecoveryAttempted = useRef(false);

  useEffect(() => {
    if (effectiveUser) {
      transientRecoveryAttempted.current = false;
      authStorage.markSession();
      tenantStorage.persistBootstrap({
        tenantId: effectiveUser.tenantId,
        tenantSlug: effectiveUser.tenantSlug,
      });
    }
  }, [effectiveUser]);

  useEffect(() => {
    if (!isBrowser || !isError || !error) {
      return;
    }
    if (isAuthHttpError(error)) {
      return;
    }
    if (transientRecoveryAttempted.current) {
      return;
    }
    transientRecoveryAttempted.current = true;
    void refetch();
  }, [isBrowser, isError, error, refetch]);

  const { mutateAsync: logoutMutation } = usePostApiAuthLogout();

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    // API only — no automatic logout here; callers show success UI then redirect.
    const data = await customInstance<{ success?: boolean; message?: string }>({
      url: '/api/UserManagement/me/password',
      method: 'PUT',
      data: { currentPassword, newPassword },
    });
    return data;
  }, []);

  const logout = useCallback(
    async (options?: { silent?: boolean; redirectTo?: string }) => {
      try {
        await logoutMutation();
      } catch (logoutError) {
        technicalConsole.error('Logout request failed', logoutError);
      } finally {
        authStorage.removeToken();
        queryClient.setQueryData(AUTH_KEYS.user, null);
        queryClient.clear();
        router.replace(options?.redirectTo ?? '/login');
        if (!options?.silent) {
          message.success(t('common.auth.logoutSuccess'));
        }
      }
    },
    [logoutMutation, queryClient, router, message, t]
  );

  /**
   * Rotates the refresh token (HttpOnly cookie). Pass `tenantId` after a header tenant switch so JWT `tenant_id` matches.
   * @returns true when refresh succeeded.
   */
  const refreshToken = useCallback(
    async (tenantId?: string | null): Promise<boolean> => {
      const next = await refreshAccessToken({
        tenantId,
        clearOnFailure: tenantId == null || tenantId.trim() === '',
      });
      if (!next) {
        return false;
      }
      await queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user });
      return true;
    },
    [queryClient]
  );

  let authStatus: AuthStatus = AuthStatus.Loading;

  const meInFlight =
    !effectiveUser && (isFetching || fetchStatus === 'fetching');

  if (effectiveUser) {
    authStatus = AuthStatus.Authenticated;
  } else if (!querySettled) {
    authStatus = AuthStatus.Loading;
  } else if (meInFlight) {
    // After login: prior /me error can stay until refetch completes — avoid flashing Unauthenticated.
    authStatus = AuthStatus.Loading;
  } else if (isError && !isAuthHttpError(error)) {
    authStatus = AuthStatus.Loading;
  } else if (isError && isAuthHttpError(error)) {
    authStatus = AuthStatus.Unauthenticated;
  } else {
    authStatus = AuthStatus.Unauthenticated;
  }

  const isAuthInitializing =
    !isBrowser ||
    !querySettled ||
    Boolean(isError && !isAuthHttpError(error)) ||
    Boolean(meInFlight);

  const isInitialized = !isAuthInitializing;

  const lastLoggedStatus = useRef<string | null>(null);

  if (process.env.NODE_ENV === 'development' && lastLoggedStatus.current !== authStatus) {
    technicalConsole.devLog(
      `[useAuth] authStatus=${authStatus} isAuthInitializing=${isAuthInitializing} fetchStatus=${fetchStatus} isFetching=${isFetching}`
    );
    lastLoggedStatus.current = authStatus;
  }

  const mustChangePassword = effectiveUser?.mustChangePasswordOnNextLogin;
  const checkPasswordChangeRequired = mustChangePassword === true;

  return {
    user: effectiveUser,
    userPermissions: effectiveUser?.permissions ?? [],
    authStatus,
    isAuthInitializing,
    /** False until browser mount and /me bootstrap completes (no early permission redirects). */
    isInitialized,
    isAuthenticated: authStatus === AuthStatus.Authenticated,
    isLoadingAuth: authStatus === AuthStatus.Loading,
    isLoading: authStatus === AuthStatus.Loading,
    mustChangePassword,
    checkPasswordChangeRequired,
    error,
    logout,
    changePassword,
    refreshToken,
    refetchMe: refetch,
  };
};
