'use client';

import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';

import { AUTH_KEYS } from '@/features/auth/hooks/useAuth';
import { AUTH_SESSION_CLEARED_EVENT } from '@/features/auth/services/authStorage';

/**
 * Keeps TanStack Query auth state aligned when tokens are cleared outside React (e.g. axios refresh failure).
 */
export function AuthSessionInvalidationListener() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const onCleared = () => {
      queryClient.setQueryData(AUTH_KEYS.user, null);
    };
    window.addEventListener(AUTH_SESSION_CLEARED_EVENT, onCleared);
    return () => window.removeEventListener(AUTH_SESSION_CLEARED_EVENT, onCleared);
  }, [queryClient]);

  return null;
}
