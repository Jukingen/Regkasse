'use client';

/**
 * Auth session context — bridges React Query `/me` bootstrap to consumers that expect a provider.
 * Permissions come from GET `/api/Auth/me` (not a separate admin permissions API).
 *
 * Login flow remains in `LoginForm` (Orval login + `fetchAuthUser` cache warm).
 */

import React, { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { refreshUserPermissions } from '@/features/auth/api/fetchUserPermissions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import type { AuthUser } from '@/shared/auth/types';

export type AuthContextType = {
  user: AuthUser | null;
  userPermissions: string[];
  isAuthenticated: boolean;
  isLoading: boolean;
  logout: (options?: { silent?: boolean; redirectTo?: string }) => Promise<void>;
  refetchMe: () => Promise<unknown>;
  /** Re-fetch `/me` and return updated permission strings. */
  refreshUserPermissions: () => Promise<string[]>;
};

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const { user, isAuthenticated, isLoading, logout, refetchMe } = useAuth();

  const userPermissions = useMemo(() => user?.permissions ?? [], [user?.permissions]);

  const value = useMemo<AuthContextType>(
    () => ({
      user: user ?? null,
      userPermissions,
      isAuthenticated,
      isLoading,
      logout,
      refetchMe,
      refreshUserPermissions: () => refreshUserPermissions(queryClient),
    }),
    [user, userPermissions, isAuthenticated, isLoading, logout, refetchMe, queryClient],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuthContext(): AuthContextType {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuthContext must be used within AuthProvider');
  }
  return ctx;
}
