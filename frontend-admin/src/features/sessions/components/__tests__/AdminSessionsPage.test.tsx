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
  },
}));

vi.mock('@/features/sessions/hooks/useAdminSessions', () => ({
  useAdminSessions: () => sessionsQuery.current,
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ isSuperAdmin: true }),
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
  const fromNow = () => '1 minute ago';
  const dayjs = () => ({ fromNow });
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

const sampleSession: AdminActiveSession = {
  id: 'sess-1',
  userId: 'u1',
  userName: 'cashier1',
  displayName: 'Anna Kassier',
  role: 'Cashier',
  clientApp: 'POS',
  deviceName: 'iPad',
  browser: 'Safari',
  os: 'iOS',
  ipAddress: '192.168.1.10',
  startedAtUtc: '2026-08-22T08:00:00.000Z',
  lastActivityAtUtc: '2026-08-22T08:01:00.000Z',
  isActive: true,
  isCurrent: false,
};

describe('AdminSessionsPage', () => {
  beforeEach(() => {
    sessionsQuery.current = {
      sessions: [sampleSession],
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      terminateOne: { isPending: false, mutateAsync: vi.fn() },
      terminateAll: { isPending: false, mutateAsync: vi.fn() },
    };
  });

  it('shows browser and OS columns from the session DTO', () => {
    render(<AdminSessionsPage />);

    expect(screen.getByText('users.sessions.colBrowser')).toBeTruthy();
    expect(screen.getByText('users.sessions.colOs')).toBeTruthy();
    expect(screen.getByText('Safari')).toBeTruthy();
    expect(screen.getByText('iOS')).toBeTruthy();
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
    expect(screen.queryByText('Safari')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'common.buttons.retry' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });
});
