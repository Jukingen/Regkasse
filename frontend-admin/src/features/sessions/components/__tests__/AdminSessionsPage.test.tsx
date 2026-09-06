/**
 * @vitest-environment jsdom
 */
import { fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminActiveSession } from '@/api/manual/adminSessions';

import { AdminSessionsPage } from '../AdminSessionsPage';

const sessionsQuery = vi.hoisted(() => ({
  current: {
    sessions: [] as AdminActiveSession[],
    isLoading: false,
    isFetching: false,
    isError: false,
    error: null as Error | null,
    refetch: vi.fn(),
    terminateOne: { isPending: false, mutateAsync: vi.fn() },
    terminateAll: { isPending: false, mutateAsync: vi.fn() },
    terminateBulk: { isPending: false, mutateAsync: vi.fn() },
  },
}));

const permissions = vi.hoisted(() => ({
  current: {
    isSuperAdmin: true,
    isManager: false,
    hasPermission: (_key: string) => true,
  },
}));

vi.mock('@/features/sessions/hooks/useAdminSessions', () => ({
  useAdminSessions: () => sessionsQuery.current,
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => permissions.current,
}));

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
  useTenantList: () => ({ tenants: [], isLoading: false }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    successKey: vi.fn(),
    success: vi.fn(),
    errorKey: vi.fn(),
  }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock('@/lib/dayjs', () => {
  const dayjs = () => ({ format: () => '10:25' });
  return { default: dayjs };
});

vi.mock('@/shared/adminPlatformBreadcrumbs', () => ({
  buildPlatformAdminBreadcrumbs: () => [],
}));

vi.mock('@/components/admin-layout/AdminPageHeader', () => ({
  AdminPageHeader: ({ title }: { title: ReactNode }) => <h1>{title}</h1>,
}));

vi.mock('@/components/admin-layout/AdminPageShell', () => ({
  AdminPageShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock('@/features/access/components/AccessSecondaryNav', () => ({
  AccessSecondaryNav: () => <nav data-testid="access-secondary-nav" />,
}));

vi.mock('@/components/ConfirmDialog', () => ({
  ConfirmDialog: () => null,
}));

vi.mock('@/components/EmptyState', () => ({
  EmptyState: ({ title }: { title: string }) => <div>{title}</div>,
}));

vi.mock('@/components/StatusBadge', () => ({
  StatusBadge: ({ label }: { label: string }) => <span>{label}</span>,
}));

vi.mock('@/shared/errors/ApiErrorAlertDescription', () => ({
  ApiErrorAlertDescription: ({ fallbackKey }: { fallbackKey: string }) => <span>{fallbackKey}</span>,
}));

vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

const posSession: AdminActiveSession = {
  id: 'sess-1',
  userId: 'u1',
  userName: 'cashier1',
  displayName: 'Anna Kassier',
  role: 'Cashier',
  clientApp: 'pos',
  deviceName: 'iPad',
  platformLabel: 'POS (Android)',
  browser: 'Mobile App',
  os: 'Android',
  ipAddress: '192.168.1.2',
  startedAtUtc: '2026-08-22T08:00:00.000Z',
  lastActivityAtUtc: '2026-08-22T08:25:00.000Z',
  durationSeconds: 5,
  isActive: true,
  isCurrent: false,
};

const currentAdminSession: AdminActiveSession = {
  id: 'sess-admin',
  userId: 'sa-1',
  userName: 'admin',
  role: 'SuperAdmin',
  clientApp: 'admin',
  platformLabel: 'Admin',
  browser: 'Chrome',
  os: 'Windows',
  ipAddress: '84.112.81.99',
  startedAtUtc: '2026-08-22T08:00:00.000Z',
  lastActivityAtUtc: '2026-08-22T08:30:00.000Z',
  durationSeconds: 2,
  isActive: true,
  isCurrent: true,
};

describe('AdminSessionsPage', () => {
  beforeEach(() => {
    permissions.current = {
      isSuperAdmin: true,
      isManager: false,
      hasPermission: () => true,
    };
    sessionsQuery.current = {
      sessions: [currentAdminSession, posSession],
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      terminateOne: { isPending: false, mutateAsync: vi.fn() },
      terminateAll: { isPending: false, mutateAsync: vi.fn() },
      terminateBulk: { isPending: false, mutateAsync: vi.fn() },
    };
  });

  it('matches the expected session table layout', () => {
    render(<AdminSessionsPage />);

    expect(screen.getByText('users.sessions.filterUser')).toBeTruthy();
    expect(screen.getByText('users.sessions.filterRole')).toBeTruthy();
    expect(screen.getByText('users.sessions.filterTenant')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'users.sessions.searchButton' })).toBeTruthy();

    expect(screen.getByText('admin')).toBeTruthy();
    expect(screen.getByText('Windows - Chrome')).toBeTruthy();
    expect(screen.getByText('cashier1')).toBeTruthy();
    expect(screen.getByText('POS (Android)')).toBeTruthy();
    expect(screen.getByText('2s')).toBeTruthy();
    expect(screen.getByText('5s')).toBeTruthy();
    expect(screen.getByText('users.sessions.thisDevice')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'users.sessions.logout' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'users.sessions.logoutBulk' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'users.sessions.logoutAllOthers' })).toBeTruthy();
  });

  it('shows an error alert with retry instead of an empty table', () => {
    const refetch = vi.fn();
    sessionsQuery.current = {
      ...sessionsQuery.current,
      sessions: [],
      isError: true,
      error: new Error('network'),
      refetch,
    };

    render(<AdminSessionsPage />);

    expect(screen.getByText('users.sessions.loadFailed')).toBeTruthy();
    expect(screen.getByText('users.sessions.loadFailedHint')).toBeTruthy();
    expect(screen.queryByText('POS (Android)')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'common.buttons.retry' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it('hides logout actions when the caller only has user.view', () => {
    permissions.current = {
      isSuperAdmin: false,
      isManager: true,
      hasPermission: (key: string) => key === 'user.view',
    };

    render(<AdminSessionsPage />);

    expect(screen.getByText('POS (Android)')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'users.sessions.logout' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'users.sessions.logoutAllOthers' })).toBeNull();
  });

  it('blocks cashiers without user.view', () => {
    permissions.current = {
      isSuperAdmin: false,
      isManager: false,
      hasPermission: () => false,
    };

    render(<AdminSessionsPage />);

    expect(screen.getByText('users.sessions.accessDeniedDescription')).toBeTruthy();
    expect(screen.queryByText('POS (Android)')).toBeNull();
  });
});
