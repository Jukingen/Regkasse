import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { PermissionRouteGuard } from '@/shared/auth/PermissionRouteGuard';

const harness = vi.hoisted(() => ({
  pathname: '/admin/rooms',
  profileId: 'vet',
  posFeatures: {} as Record<string, boolean>,
  isLoading: false,
}));

vi.mock('next/navigation', () => ({
  usePathname: () => harness.pathname,
  useRouter: () => ({ back: vi.fn(), push: vi.fn(), replace: vi.fn() }),
}));

vi.mock('@/features/auth/hooks/useAuth', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/hooks/useAuth')>();
  return {
    ...actual,
    useAuth: () => ({
      user: {
        id: 'u1',
        role: 'Manager',
        permissions: ['product.view', 'table.view', 'settings.view'],
      },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    }),
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

vi.mock('@/features/vertical-profiles/contexts/AdminVerticalProfileContext', () => ({
  useAdminVerticalProfile: () => ({
    profileId: harness.profileId,
    posFeatures: harness.posFeatures,
    posLayout: 'standard',
    isLoading: harness.isLoading,
  }),
}));

function renderGuard() {
  return render(
    <PermissionRouteGuard>
      <div>Protected</div>
    </PermissionRouteGuard>
  );
}

describe('PermissionRouteGuard vertical profile', () => {
  beforeEach(() => {
    harness.pathname = '/admin/rooms';
    harness.profileId = 'vet';
    harness.posFeatures = {};
    harness.isLoading = false;
  });

  it('blocks a deep link that the ambient profile cannot see', () => {
    renderGuard();
    expect(screen.getByText('common.system.forbidden403Title')).toBeInTheDocument();
    expect(screen.queryByText('Protected')).not.toBeInTheDocument();
  });

  it('allows the same route when the profile matches', () => {
    harness.profileId = 'beherbergung';
    harness.posFeatures = { roomTracking: true };
    renderGuard();
    expect(screen.getByText('Protected')).toBeInTheDocument();
  });

  it('blocks a feature-gated route when the feature is off', () => {
    harness.pathname = '/tables';
    harness.profileId = 'gastronomy';
    harness.posFeatures = { tables: false, kitchenDisplay: true };
    renderGuard();
    expect(screen.getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });

  it('waits instead of rendering a gated route while the profile loads', () => {
    harness.isLoading = true;
    renderGuard();
    expect(screen.queryByText('Protected')).not.toBeInTheDocument();
    expect(screen.queryByText('common.system.forbidden403Title')).not.toBeInTheDocument();
  });

  it('keeps a universal route available for every profile', () => {
    harness.pathname = '/dashboard';
    harness.profileId = 'taxi';
    renderGuard();
    expect(screen.getByText('Protected')).toBeInTheDocument();
  });
});
