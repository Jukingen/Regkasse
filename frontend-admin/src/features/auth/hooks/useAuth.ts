'use client';

import { useAntdApp } from '@/hooks/useAntdApp';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';
import { customInstance } from '@/lib/axios';
import { usePostApiAuthLogout } from '@/api/generated/auth/auth';

import { authStorage } from '@/features/auth/services/authStorage';
import { tenantStorage } from '@/features/auth/services/tenantStorage';
import { mapMeResponseToAuthUser, type MeResponse } from '@/features/auth/utils/mapMeResponseToAuthUser';
import type { AuthUser } from '@/shared/auth/types';
import { technicalConsole } from '@/shared/dev/technicalConsole';
import { useI18n } from '@/i18n';

// Define the key for the user query
export const AUTH_KEYS = {
    user: ['auth', 'me'] as const,
};

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

    const isBrowser = useSyncExternalStore(emptySubscribe, () => true, () => false);
    const hasCredentials = isBrowser && authStorage.hasToken();

    const { data: user, isError, error, refetch, isFetched, isFetching, fetchStatus } = useQuery({
        queryKey: AUTH_KEYS.user,
        queryFn: fetchAuthUser,
        retry: false, // Strictly no retries for /me (refresh handled inside axios for 401)
        staleTime: 1000 * 30, // 30 seconds
        gcTime: 1000 * 60 * 10,
        refetchOnWindowFocus: false,
        refetchOnMount: false,
        enabled: isBrowser && hasCredentials,
    });

    const effectiveUser = hasCredentials ? user : undefined;
    const querySettled = isFetched || isError || (isBrowser && !hasCredentials);

    const transientRecoveryAttempted = useRef(false);

    useEffect(() => {
        if (effectiveUser) {
            transientRecoveryAttempted.current = false;
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
        if (!hasCredentials || isAuthHttpError(error)) {
            return;
        }
        if (transientRecoveryAttempted.current) {
            return;
        }
        transientRecoveryAttempted.current = true;
        void refetch();
    }, [isBrowser, isError, hasCredentials, error, refetch]);

    const { mutateAsync: logoutMutation } = usePostApiAuthLogout();

    const logout = useCallback(async () => {
        try {
            await logoutMutation();
        } catch (logoutError) {
            technicalConsole.error('Logout request failed', logoutError);
        } finally {
            authStorage.removeToken();
            queryClient.setQueryData(AUTH_KEYS.user, null);
            queryClient.clear();
            router.replace('/login');
            message.success(t('common.auth.logoutSuccess'));
        }
    }, [logoutMutation, queryClient, router, t]);

    let authStatus: AuthStatus = AuthStatus.Loading;

    const meInFlightWithCredentials =
        hasCredentials && !effectiveUser && (isFetching || fetchStatus === 'fetching');

    if (effectiveUser) {
        authStatus = AuthStatus.Authenticated;
    } else if (!querySettled) {
        authStatus = AuthStatus.Loading;
    } else if (meInFlightWithCredentials) {
        // After login or token change: prior /me error can stay until refetch completes — avoid flashing Unauthenticated.
        authStatus = AuthStatus.Loading;
    } else if (isError && hasCredentials && !isAuthHttpError(error)) {
        authStatus = AuthStatus.Loading;
    } else if (isError && isAuthHttpError(error)) {
        authStatus = AuthStatus.Unauthenticated;
    } else if (isError && !hasCredentials) {
        authStatus = AuthStatus.Unauthenticated;
    } else {
        authStatus = AuthStatus.Unauthenticated;
    }

    const isAuthInitializing =
        !isBrowser ||
        !querySettled ||
        (Boolean(isError && hasCredentials && !isAuthHttpError(error))) ||
        Boolean(meInFlightWithCredentials);

    const isInitialized = !isAuthInitializing;

    const lastLoggedStatus = useRef<string | null>(null);

    if (process.env.NODE_ENV === 'development' && lastLoggedStatus.current !== authStatus) {
        technicalConsole.devLog(
            `[useAuth] authStatus=${authStatus} isAuthInitializing=${isAuthInitializing} fetchStatus=${fetchStatus} isFetching=${isFetching} hasCredentials=${hasCredentials}`,
        );
        lastLoggedStatus.current = authStatus;
    }

    return {
        user: effectiveUser,
        authStatus,
        isAuthInitializing,
        /** False until browser mount and /me bootstrap completes (no early permission redirects). */
        isInitialized,
        isAuthenticated: authStatus === AuthStatus.Authenticated,
        isLoadingAuth: authStatus === AuthStatus.Loading,
        error,
        logout,
        refetchMe: refetch,
    };
};
