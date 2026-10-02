import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { MonatsbelegList } from '@/features/rksv/MonatsbelegList';
import { MonatsbelegPolicyInlineEditor } from '@/features/rksv/components/MonatsbelegPolicyInlineEditor';
import { I18nProvider } from '@/i18n';

const fetchMonatsbelege = vi.hoisted(() => vi.fn());
const fetchMonatsbelegPolicy = vi.hoisted(() => vi.fn());
const putMonatsbelegPolicy = vi.hoisted(() => vi.fn());
const mutateAsync = vi.hoisted(() => vi.fn());

vi.mock('@/features/rksv/api/monatsbelege', () => ({
  monatsbelegeListQueryKey: ['admin', 'rksv', 'monatsbelege'],
  fetchMonatsbelege: (...args: unknown[]) => fetchMonatsbelege(...args),
}));

vi.mock('@/features/settings/api/monatsbelegPolicy', () => ({
  monatsbelegPolicyQueryKey: ['admin', 'rksv', 'monatsbeleg-policy'],
  fetchMonatsbelegPolicy: (...args: unknown[]) => fetchMonatsbelegPolicy(...args),
  putMonatsbelegPolicy: (...args: unknown[]) => putMonatsbelegPolicy(...args),
}));

vi.mock('@/features/rksv/hooks/useMonatsbeleg', () => ({
  useMonatsbelegStatus: () => ({
    data: [
      {
        cashRegisterId: 'reg-1',
        status: {
          blockingMode: 'GracePeriod',
          lastMonthMissing: false,
          missingMonths: [
            { year: 2026, month: 3, isOverdue: false, deadline: '2026-04-07' },
            { year: 2026, month: 4, isOverdue: true, deadline: '2026-05-07' },
          ],
        },
      },
    ],
    isLoading: false,
    refetch: vi.fn(),
  }),
}));

vi.mock('@/features/cash-registers/hooks/useAdminCashRegisterList', () => ({
  useAdminCashRegisterList: () => ({
    registers: [{ id: 'reg-1', registerNumber: 'Kasse-1', location: 'Theke' }],
    isLoading: false,
    isFetching: false,
    refetch: vi.fn(),
  }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

vi.mock('@/shared/auth/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn(), open: vi.fn() },
    modal: { confirm: vi.fn() },
    notification: { error: vi.fn(), success: vi.fn() },
  }),
}));

vi.mock('@/features/rksv/hooks/useCreateMonatsbeleg', () => ({
  useCreateMonatsbeleg: () => ({ mutateAsync }),
}));

vi.mock('@/features/rksv/components/CreateMonatsbelegModal', () => ({
  CreateMonatsbelegModal: () => null,
}));

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', ResizeObserverMock);
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

function listRow(overrides: Record<string, unknown>) {
  return {
    paymentId: 'pay-1',
    cashRegisterId: 'reg-1',
    registerNumber: 'Kasse-1',
    registerLocation: 'Theke',
    year: 2026,
    month: 1,
    period: '2026-01',
    createdAtUtc: '2026-02-01T10:00:00Z',
    createdBy: 'system',
    createdByUserId: 'system',
    tseSignature: 'aaa.bbb.ccc',
    depStatus: 'InDep',
    fonStatus: 'NotRequired',
    isJahresbeleg: false,
    autoCreated: false,
    status: 'created',
    lastError: null,
    attemptCount: 0,
    correlationId: null,
    ...overrides,
  };
}

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <App>{ui}</App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('Monatsbeleg / Jahresbeleg FA operations screen', () => {
  beforeEach(() => {
    fetchMonatsbelege.mockReset();
    fetchMonatsbelegPolicy.mockReset();
    putMonatsbelegPolicy.mockReset();
    mutateAsync.mockReset();
    fetchMonatsbelegPolicy.mockResolvedValue({
      blockingMode: 'GracePeriod',
      autoMonatsbelegEnabled: true,
      monatsbelegRetryCount: 3,
      useDecemberMonatsbelegAsJahresbeleg: true,
    });
    fetchMonatsbelege.mockResolvedValue({
      year: 2026,
      total: 2,
      hasFailedAutoCreates: false,
      hasMissedAutoCreates: false,
      items: [
        listRow({ paymentId: 'pay-jan', month: 1, period: '2026-01', autoCreated: false }),
        listRow({
          paymentId: 'pay-feb',
          month: 2,
          period: '2026-02',
          autoCreated: true,
          createdBy: 'system',
        }),
      ],
    });
    putMonatsbelegPolicy.mockResolvedValue({
      blockingMode: 'GracePeriod',
      autoMonatsbelegEnabled: true,
      monatsbelegRetryCount: 3,
      useDecemberMonatsbelegAsJahresbeleg: true,
    });
  });

  it('renders Created, Missing, Auto-created and Overdue statuses with gate and auto-create', async () => {
    renderWithProviders(<MonatsbelegList />);

    expect(await screen.findByTestId('monatsbeleg-display-Created')).toBeInTheDocument();
    expect(screen.getByTestId('monatsbeleg-display-AutoCreated')).toBeInTheDocument();
    expect(screen.getByTestId('monatsbeleg-display-Missing')).toBeInTheDocument();
    expect(screen.getByTestId('monatsbeleg-display-Overdue')).toBeInTheDocument();
    expect(screen.getAllByTestId('monatsbeleg-gate-GracePeriod').length).toBeGreaterThan(0);
    expect(screen.getByTestId('monatsbeleg-auto-Success')).toBeInTheDocument();
    expect(screen.getAllByTestId('monatsbeleg-auto-Missed').length).toBeGreaterThan(0);
    expect(screen.getByTestId('monatsbeleg-bulk-create-missing')).toBeInTheDocument();
  });

  it('shows the Jahresbelege tab FON Belegcheck reminder', async () => {
    renderWithProviders(<MonatsbelegList />);
    await screen.findByTestId('monatsbeleg-ops-table');
    fireEvent.click(screen.getByRole('tab', { name: 'Jahresbelege' }));
    expect(await screen.findByTestId('jahresbeleg-fon-reminder')).toBeInTheDocument();
    expect(screen.getByText(/15\.02\.2027/)).toBeInTheDocument();
  });

  it('saves the inline Monatsbeleg policy editor', async () => {
    renderWithProviders(<MonatsbelegPolicyInlineEditor />);

    expect(await screen.findByTestId('monatsbeleg-policy-save')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('monatsbeleg-policy-save'));

    await waitFor(() => {
      expect(putMonatsbelegPolicy.mock.calls[0]?.[0]).toEqual({
        blockingMode: 'GracePeriod',
        autoMonatsbelegEnabled: true,
      });
    });
  });
});
