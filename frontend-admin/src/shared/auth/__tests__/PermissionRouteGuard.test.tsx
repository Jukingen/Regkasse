/**
 * PermissionRouteGuard: fail-closed when no permissions or insufficient permission for route.
 */
import '@testing-library/jest-dom';
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { PermissionRouteGuard } from '../PermissionRouteGuard';

const mockReplace = vi.fn();
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ replace: mockReplace }),
}));

const mockUseAuth = vi.fn();
vi.mock('@/features/auth/hooks/useAuth', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/hooks/useAuth')>();
  return {
    ...actual,
    useAuth: () => mockUseAuth(),
  };
});

vi.mock('@/shared/auth/routeGuardConfig', () => ({
  ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS: false,
}));

describe('PermissionRouteGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('redirects to /403 when user has no permissions (fail-closed)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: [] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(mockReplace).toHaveBeenCalledWith('/403');
  });

  it('redirects to /403 when user has insufficient permission for path', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: ['product.view'] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(mockReplace).toHaveBeenCalledWith('/403');
  });

  it('renders children when user has required permission for path', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Admin', permissions: ['settings.view'] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(getByText('Protected')).toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });
});
