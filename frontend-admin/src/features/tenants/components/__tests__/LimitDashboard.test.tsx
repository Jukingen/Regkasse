import { fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { LimitDashboardDto } from '@/features/tenants/api/tenantLimits';
import LimitDashboard from '../LimitDashboard';

const push = vi.fn();

const { dashboard, dashboardQuery } = vi.hoisted(() => {
  const data = {
    lastUpdated: '2026-08-22T08:00:00.000Z',
    summary: { total: 9, healthy: 7, warning: 1, critical: 1 },
    limits: [
      {
        tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantName: 'Cafe',
        key: 'maxProductsPerTenant',
        displayName: 'Max. products per tenant',
        description: 'Active catalog products.',
        current: 8,
        limit: 10,
        percentage: 80,
        status: 'Warning' as const,
        trend: 'Increasing' as const,
        changeCount: 3,
        changeUnit: 'products',
      },
    ],
    criticalUsers: [
      {
        tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantName: 'Cafe',
        userId: 'u1',
        userName: 'cashier1',
        displayName: 'Anna Kassier',
        role: 'Cashier',
        limitKey: 'maxActiveRegistersPerUser',
        limit: 1,
        current: 1,
        percentage: 100,
        status: 'Full' as const,
        recommendedAction: 'Unassign a register before assigning another.',
      },
    ],
    recentActivity: [
      {
        id: 'log-1',
        timestampUtc: '2026-08-22T08:00:00.000Z',
        tenantId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantName: 'Cafe',
        limitKey: 'maxProductsPerTenant',
        eventType: 'LimitApproaching',
        status: 'Warning',
        description: 'Limit maxProductsPerTenant is at 80%.',
        userName: null,
        isRead: false,
      },
    ],
    totalViolations: 1,
    approachingLimits: 1,
    unreadAlertCount: 2,
    allTenants: false,
  } satisfies LimitDashboardDto;

  return {
    dashboard: { current: data },
    dashboardQuery: {
      current: {
        data: data as LimitDashboardDto | undefined,
        isLoading: false,
        isFetching: false,
        isError: false,
        error: null as Error | null,
        refetch: vi.fn(),
      },
    },
  };
});

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/limits/dashboard',
  useRouter: () => ({ push }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/components/admin-layout/AdminPageHeader', () => ({
  AdminPageHeader: ({ title, extra }: { title: ReactNode; extra?: ReactNode }) => (
    <div>
      <h1>{title}</h1>
      <div>{extra}</div>
    </div>
  ),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: {
      role: 'Manager',
      userName: 'manager1',
      firstName: 'Mia',
      lastName: 'Manager',
    },
  }),
}));

vi.mock('@/hooks/useTenant', () => ({
  useTenant: () => ({
    tenant: { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', name: 'Cafe', slug: 'cafe' },
    tenants: [],
    tenantsLoading: false,
  }),
}));

vi.mock('@/hooks/useCashRegisterSelection', () => ({
  useCashRegisterSelection: () => ({
    registers: [],
    registerOptions: [],
    selectedRegister: null,
    isLoading: false,
  }),
}));

vi.mock('@/features/tenants/hooks/useTenantLimits', () => ({
  useLimitDashboard: () => dashboardQuery.current,
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    warning: vi.fn(),
    success: vi.fn(),
  }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
  }),
}));

vi.mock('@/shared/adminShellLabels', () => ({
  adminOverviewCrumb: () => ({ title: 'Overview', href: '/dashboard' }),
}));

vi.mock('@/shared/errors/ApiErrorAlertDescription', () => ({
  ApiErrorAlertDescription: ({ fallbackKey }: { fallbackKey: string }) => <span>{fallbackKey}</span>,
}));

describe('LimitDashboard', () => {
  beforeEach(() => {
    dashboardQuery.current = {
      data: dashboard.current,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    };
  });

  it('shows usage, critical users, summary, and activity', () => {
    render(<LimitDashboard />);

    expect(screen.getByText('tenants.limits.dashboard.title')).toBeTruthy();
    expect(screen.getByText('8 / 10')).toBeTruthy();
    expect(screen.getByText('Anna Kassier')).toBeTruthy();
    expect(screen.getByText('tenants.limits.dashboard.context.mandantLine')).toBeTruthy();
    expect(screen.getByText('Limit maxProductsPerTenant is at 80%.')).toBeTruthy();
    expect(screen.getByText('tenants.limits.dashboard.summary.warning')).toBeTruthy();
  });

  it('shows an error alert with retry instead of empty tables', () => {
    const refetch = vi.fn();
    dashboardQuery.current = {
      data: undefined,
      isLoading: false,
      isFetching: false,
      isError: true,
      error: new Error('network'),
      refetch,
    };

    render(<LimitDashboard />);

    expect(screen.getByText('tenants.limits.dashboard.loadFailed')).toBeTruthy();
    expect(screen.getByText('tenants.limits.dashboard.loadFailedHint')).toBeTruthy();
    expect(screen.queryByText('Anna Kassier')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'common.buttons.retry' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });
});
