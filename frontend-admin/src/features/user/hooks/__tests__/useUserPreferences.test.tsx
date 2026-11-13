import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useUserPreferences } from '@/features/user/hooks/useUserPreferences';

const mockUseAuth = vi.fn();
const mockUsePermissions = vi.fn();
const mockFetchUserPreferences = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

vi.mock('@/lib/personalization/userPreferencesApi', () => ({
  userPreferencesQueryKey: ['user-preferences'],
  fetchUserPreferences: () => mockFetchUserPreferences(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useUserPreferences', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
    mockUsePermissions.mockReset();
    mockFetchUserPreferences.mockReset();
  });

  it('fetches preferences for any authenticated role', async () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Cashier', permissions: [] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => false,
      hasAnyPermission: () => false,
    });
    mockFetchUserPreferences.mockResolvedValue({
      themeMode: 'system',
      densityMode: 'standard',
      defaultPage: '/dashboard',
      reducedAnimations: false,
    });

    const { result } = renderHook(() => useUserPreferences(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.data?.defaultPage).toBe('/dashboard');
    });

    expect(mockFetchUserPreferences).toHaveBeenCalledTimes(1);
    expect(result.current.isAuthorized).toBe(true);
  });

  it('does not fetch before auth initializes', async () => {
    mockUseAuth.mockReturnValue({
      user: undefined,
      isInitialized: false,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => false,
      hasAnyPermission: () => false,
    });

    const { result } = renderHook(() => useUserPreferences(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => {
      expect(result.current.fetchStatus).toBe('idle');
    });

    expect(mockFetchUserPreferences).not.toHaveBeenCalled();
    expect(result.current.isAuthorized).toBe(false);
  });
});
