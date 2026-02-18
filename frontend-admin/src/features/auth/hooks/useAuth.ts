import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { useCallback, useRef } from 'react';
import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';
import { usePostApiAuthLogout } from '@/api/generated/auth/auth';
import { UserInfo } from '@/api/generated/model';
import { message } from 'antd';
import { authStorage } from '@/features/auth/services/authStorage';

// Define the key for the user query
export const AUTH_KEYS = {
    user: ['auth', 'me'] as const,
};

// Manually define the fetcher for /api/Auth/me since it might not be generated
const fetchUser = async (): Promise<UserInfo> => {
    if (process.env.NODE_ENV === 'development') {
        console.log('📡 [API] Fetching /api/Auth/me');
    }
    return customInstance<UserInfo>({
        url: '/api/Auth/me',
        method: 'GET',
    });
};

export enum AuthStatus {
    Loading = 'loading',
    Authenticated = 'authenticated',
    Unauthenticated = 'unauthenticated',
}

export const useAuth = () => {
    const queryClient = useQueryClient();
    const router = useRouter();

    const { data: user, isLoading, isError, error, refetch, isFetched } = useQuery({
        queryKey: AUTH_KEYS.user,
        queryFn: fetchUser,
        retry: false, // Strictly no retries for /me
        staleTime: 1000 * 30, // 30 seconds
        gcTime: 1000 * 60 * 10,
        refetchOnWindowFocus: false,
        refetchOnMount: false,
    });

    const { mutateAsync: logoutMutation } = usePostApiAuthLogout();

    const logout = useCallback(async () => {
        try {
            await logoutMutation();
        } catch (error) {
            console.error('Logout failed', error);
        } finally {
            // Clear storage and cache
            authStorage.removeToken();
            queryClient.setQueryData(AUTH_KEYS.user, null);
            queryClient.clear(); // Clear everything to be safe
            router.replace('/login');
            message.success('Logged out successfully');
        }
    }, [logoutMutation, queryClient, router]);

    // Strict Status Logic
    let authStatus: AuthStatus = AuthStatus.Loading;

    // Check for 401/403 specifically in the error object
    const isAuthError = error && (
        (error as any)?.response?.status === 401 ||
        (error as any)?.response?.status === 403
    );

    if (!isLoading && isFetched && user) {
        authStatus = AuthStatus.Authenticated;
    } else if (isError || (isFetched && !user) || isAuthError) {
        authStatus = AuthStatus.Unauthenticated;
    }

    const isInitialized = isFetched || isError;

    // Track last logged status to prevent console spam
    const lastLoggedStatus = useRef<string | null>(null);

    // Debug logs
    if (process.env.NODE_ENV === 'development' && lastLoggedStatus.current !== authStatus) {
        console.log(`🔐 [useAuth] status: '${authStatus}'`);
        lastLoggedStatus.current = authStatus;
    }

    return {
        user,
        authStatus,
        isInitialized,
        isAuthenticated: authStatus === AuthStatus.Authenticated,
        isLoadingAuth: authStatus === AuthStatus.Loading,
        error,
        logout,
        refetchMe: refetch
    };
};
