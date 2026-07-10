/**
 * PermissionRouteGuard: fail-closed when no permissions or insufficient permission for route.
 */
import '@testing-library/jest-dom';
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { PermissionRouteGuard } from '../PermissionRouteGuard';
import { MANAGER_ADMIN_PERMISSIONS } from '@/shared/__tests__/fixtures/adminAppPermissionFixtures';

let mockPathname = '/rksv/sonderbelege';

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname,
  useRouter: () => ({ back: vi.fn(), push: vi.fn(), replace: vi.fn() }),
}));

const mockUseAuth = vi.fn();
vi.mock('@/features/auth/hooks/useAuth', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/hooks/useAuth')>();
  return {
    ...actual,
    useAuth: () => mockUseAuth(),
  };
});

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-DE',
  }),
}));

vi.mock('@/shared/auth/routeGuardConfig', () => ({
  ALLOW_EMPTY_PERMISSIONS_FOR_ROUTE_ACCESS: false,
}));

describe('PermissionRouteGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPathname = '/rksv/sonderbelege';
  });

  it('shows forbidden view when user has no permissions (fail-closed)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: [] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });

  it('shows forbidden view when user has insufficient permission for path', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: ['product.view'] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });

  it('renders children when user has required permission for path', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'SuperAdmin', permissions: ['finanzonline.manage'] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(getByText('Protected')).toBeInTheDocument();
  });

  it('Manager with oversight permissions can access /payments', () => {
    mockPathname = '/payments';
    mockUseAuth.mockReturnValue({
      user: {
        id: 'm1',
        role: 'Manager',
        permissions: [...MANAGER_ADMIN_PERMISSIONS],
      },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Payments</div>
      </PermissionRouteGuard>
    );
    expect(getByText('Payments')).toBeInTheDocument();
  });

  it('Manager with daily-closing.view can access /tagesabschluss', () => {
    mockPathname = '/tagesabschluss';
    mockUseAuth.mockReturnValue({
      user: {
        id: 'm1',
        role: 'Manager',
        permissions: [...MANAGER_ADMIN_PERMISSIONS],
      },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Tagesabschluss</div>
      </PermissionRouteGuard>
    );
    expect(getByText('Tagesabschluss')).toBeInTheDocument();
  });

  it('Manager is blocked on platform admin /admin/tenants', () => {
    mockPathname = '/admin/tenants';
    mockUseAuth.mockReturnValue({
      user: {
        id: 'm1',
        role: 'Manager',
        permissions: [...MANAGER_ADMIN_PERMISSIONS],
      },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <PermissionRouteGuard>
        <div>Tenants</div>
      </PermissionRouteGuard>
    );
    expect(getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });
});
