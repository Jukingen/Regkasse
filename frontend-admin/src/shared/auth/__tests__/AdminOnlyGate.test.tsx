/**
 * AdminOnlyGate: permission-first; fallback isSuperAdmin (SuperAdmin + legacy Admin). Redirects to /403 when not allowed.
 */
import '@testing-library/jest-dom';
import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { AuthStatus } from '@/features/auth/hooks/useAuth';
import { AdminOnlyGate } from '../AdminOnlyGate';

const mockReplace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => '/users',
}));

const mockUseAuth = vi.fn();
vi.mock('@/features/auth/hooks/useAuth', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/auth/hooks/useAuth')>();
  return {
    ...actual,
    useAuth: () => mockUseAuth(),
  };
});

describe('AdminOnlyGate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('redirects to /403 when user has no admin permission and not SuperAdmin (or legacy Admin) role', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Cashier', permissions: ['product.view'] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(mockReplace).toHaveBeenCalledWith('/403');
  });

  it('renders children when user has legacy Admin role (treated as SuperAdmin)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Admin', permissions: [] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    const { getByText } = render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(getByText('Protected content')).toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });

  it('renders children when user has admin permission (user.manage)', () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u1', role: 'Manager', permissions: ['user.manage'] },
      authStatus: AuthStatus.Authenticated,
      isInitialized: true,
    });
    const { getByText } = render(
      <AdminOnlyGate>
        <div>Protected content</div>
      </AdminOnlyGate>
    );
    expect(getByText('Protected content')).toBeInTheDocument();
    expect(mockReplace).not.toHaveBeenCalled();
  });
});
