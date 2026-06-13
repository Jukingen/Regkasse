import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider, useAuthContext } from '@/features/auth/providers/AuthProvider';

const mockUseAuth = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
  AUTH_KEYS: { user: ['auth', 'me'] },
  fetchAuthUser: vi.fn(),
}));

function wrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <AuthProvider>{children}</AuthProvider>
      </QueryClientProvider>
    );
  };
}

describe('AuthProvider', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
  });

  it('exposes userPermissions from /me user payload', async () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Manager', permissions: ['report.view', 'user.view'] },
      isAuthenticated: true,
      isLoading: false,
      logout: vi.fn(),
      refetchMe: vi.fn(),
    });

    const queryClient = new QueryClient();
    const { result } = renderHook(() => useAuthContext(), { wrapper: wrapper(queryClient) });

    await waitFor(() => {
      expect(result.current.userPermissions).toEqual(['report.view', 'user.view']);
      expect(result.current.isAuthenticated).toBe(true);
      expect(result.current.user?.id).toBe('u1');
    });
  });

  it('defaults userPermissions to empty array when missing', async () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier' },
      isAuthenticated: true,
      isLoading: false,
      logout: vi.fn(),
      refetchMe: vi.fn(),
    });

    const queryClient = new QueryClient();
    const { result } = renderHook(() => useAuthContext(), { wrapper: wrapper(queryClient) });

    await waitFor(() => {
      expect(result.current.userPermissions).toEqual([]);
    });
  });
});
