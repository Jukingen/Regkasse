'use client';

import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { AUTH_KEYS } from '@/features/auth/hooks/useAuth';
import { AUTH_SESSION_CLEARED_EVENT } from '@/features/auth/services/authStorage';

/**
 * Keeps TanStack Query auth state aligned when tokens are cleared outside React (e.g. axios refresh failure).
 */
export function AuthSessionInvalidationListener() {
    const queryClient = useQueryClient();

    useEffect(() => {
        const onCleared = () => {
            void queryClient.invalidateQueries({ queryKey: AUTH_KEYS.user });
        };
        window.addEventListener(AUTH_SESSION_CLEARED_EVENT, onCleared);
        return () => window.removeEventListener(AUTH_SESSION_CLEARED_EVENT, onCleared);
    }, [queryClient]);

    return null;
}
