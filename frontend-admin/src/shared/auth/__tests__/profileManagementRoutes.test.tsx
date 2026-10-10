import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { PermissionRouteGuard } from '@/shared/auth/PermissionRouteGuard';

const harness = vi.hoisted(() => ({
  pathname: '/admin/patients',
  profileId: 'hair-salon',
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
        permissions: [
          'patient.view',
          'appointment.view',
          'imei.view',
          'taxi.trip.view',
          'customer.view',
        ],
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
    posFeatures: {},
    posLayout: 'standard',
    isLoading: false,
    viewAllProfiles: false,
  }),
}));

const cases = [
  { path: '/admin/patients', wrong: 'hair-salon', right: 'vet' },
  { path: '/admin/appointments', wrong: 'vet', right: 'hair-salon' },
  { path: '/admin/imeis', wrong: 'vet', right: 'handy-shop' },
  { path: '/admin/taxi-trips', wrong: 'vet', right: 'taxi' },
  { path: '/admin/customer-addresses', wrong: 'vet', right: 'mobile-services' },
] as const;

describe('profile management route guard', () => {
  beforeEach(() => {
    harness.pathname = '/admin/patients';
    harness.profileId = 'hair-salon';
  });

  it.each(cases)('returns 403 for $path when the profile is $wrong', ({ path, wrong }) => {
    harness.pathname = path;
    harness.profileId = wrong;
    render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(screen.getByText('common.system.forbidden403Title')).toBeInTheDocument();
    expect(screen.queryByText('Protected')).not.toBeInTheDocument();
  });

  it.each(cases)('allows $path for $right', ({ path, right }) => {
    harness.pathname = path;
    harness.profileId = right;
    render(
      <PermissionRouteGuard>
        <div>Protected</div>
      </PermissionRouteGuard>
    );
    expect(screen.getByText('Protected')).toBeInTheDocument();
  });
});
