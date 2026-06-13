/**
 * AdminOnlyGate: permission-first; fallback isSuperAdmin. Inline forbidden when not allowed.
 */
import '@testing-library/jest-dom';
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { AdminOnlyGate } from '../AdminOnlyGate';

vi.mock('next/navigation', () => ({
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

describe('AdminOnlyGate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows forbidden view when user has no admin permission and not SuperAdmin role', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: ['product.view'] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });

  it('shows forbidden view when user has Admin role (no longer treated as SuperAdmin)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Admin', permissions: [] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(getByText('common.system.forbidden403Title')).toBeInTheDocument();
  });

  it('renders children when user has admin permission (user.manage)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Manager', permissions: ['user.manage'] },
      authStatus: AuthStatus.Authenticated,
      isAuthInitializing: false,
      isInitialized: true,
    });
    const { getByText } = render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(getByText('Protected content')).toBeInTheDocument();
  });
});
