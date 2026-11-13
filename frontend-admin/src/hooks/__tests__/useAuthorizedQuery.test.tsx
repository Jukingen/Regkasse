import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useAuthorizationGate, useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import { PERMISSIONS } from '@/shared/auth/permissions';

const mockUseAuth = vi.fn();
const mockUsePermissions = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useAuthorizationGate', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
    mockUsePermissions.mockReset();
  });

  it('denies when auth is not initialized', () => {
    mockUseAuth.mockReturnValue({ user: undefined, isInitialized: false });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => true,
      hasAnyPermission: () => true,
    });

    const { result } = renderHook(() => useAuthorizationGate({ requiredRole: 'SuperAdmin' }));

    expect(result.current.isAuthorized).toBe(false);
  });

  it('grants SuperAdmin for requiredRole SuperAdmin', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: [] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => false,
      hasAnyPermission: () => false,
    });

    const { result } = renderHook(() => useAuthorizationGate({ requiredRole: 'SuperAdmin' }));

    expect(result.current.isAuthorized).toBe(true);
  });

  it('denies Manager for requiredRole SuperAdmin', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: [PERMISSIONS.SETTINGS_VIEW] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: (p: string) => p === PERMISSIONS.SETTINGS_VIEW,
      hasAnyPermission: (perms: string[]) => perms.some((p) => p === PERMISSIONS.SETTINGS_VIEW),
    });

    const { result } = renderHook(() => useAuthorizationGate({ requiredRole: 'SuperAdmin' }));

    expect(result.current.isAuthorized).toBe(false);
  });

  it('grants when any required permission matches', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: [PERMISSIONS.SETTINGS_VIEW] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: (p: string) => p === PERMISSIONS.SETTINGS_VIEW,
      hasAnyPermission: (perms: string[]) => perms.some((p) => p === PERMISSIONS.SETTINGS_VIEW),
    });

    const { result } = renderHook(() =>
      useAuthorizationGate({
        requiredPermission: [PERMISSIONS.SETTINGS_VIEW, PERMISSIONS.USER_MANAGE],
      })
    );

    expect(result.current.isAuthorized).toBe(true);
  });

  it('grants everyone when requiredRole is an empty array', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Cashier', permissions: [] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => false,
      hasAnyPermission: () => false,
    });

    const { result } = renderHook(() => useAuthorizationGate({ requiredRole: [] }));

    expect(result.current.isAuthorized).toBe(true);
  });
});

describe('useAuthorizedQuery', () => {
  const queryFn = vi.fn(async () => 'payload');

  beforeEach(() => {
    mockUseAuth.mockReset();
    mockUsePermissions.mockReset();
    queryFn.mockClear();
  });

  it('does not run queryFn when user lacks required role', async () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: [] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => false,
      hasAnyPermission: () => false,
    });

    const { result } = renderHook(
      () =>
        useAuthorizedQuery({
          queryKey: ['authorized', 'blocked'],
          queryFn,
          requiredRole: 'SuperAdmin',
        }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => {
      expect(result.current.fetchStatus).toBe('idle');
    });

    expect(queryFn).not.toHaveBeenCalled();
    expect(result.current.isAuthorized).toBe(false);
  });

  it('runs queryFn when user has required permission', async () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: [PERMISSIONS.SETTINGS_VIEW] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: (p: string) => p === PERMISSIONS.SETTINGS_VIEW,
      hasAnyPermission: (perms: string[]) => perms.some((p) => p === PERMISSIONS.SETTINGS_VIEW),
    });

    const { result } = renderHook(
      () =>
        useAuthorizedQuery({
          queryKey: ['authorized', 'allowed'],
          queryFn,
          requiredPermission: PERMISSIONS.SETTINGS_VIEW,
        }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => {
      expect(result.current.data).toBe('payload');
    });

    expect(queryFn).toHaveBeenCalledTimes(1);
    expect(result.current.isAuthorized).toBe(true);
  });

  it('respects enabled=false even when authorized', async () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: [] },
      isInitialized: true,
    });
    mockUsePermissions.mockReturnValue({
      hasPermission: () => true,
      hasAnyPermission: () => true,
    });

    const { result } = renderHook(
      () =>
        useAuthorizedQuery({
          queryKey: ['authorized', 'disabled'],
          queryFn,
          requiredRole: 'SuperAdmin',
          enabled: false,
        }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => {
      expect(result.current.fetchStatus).toBe('idle');
    });

    expect(queryFn).not.toHaveBeenCalled();
    expect(result.current.isAuthorized).toBe(true);
  });
});
