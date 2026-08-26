import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminOnlinePaymentDto } from '@/features/online-payments/api/onlinePaymentsApi';
import { OnlinePaymentsPage } from '@/features/online-payments/components/OnlinePaymentsPage';
import { I18nProvider } from '@/i18n';

const mockHasPermission = vi.fn();
const sample: AdminOnlinePaymentDto = {
  id: 'pay-9',
  tenantId: 'tenant-1',
  tenantName: 'Cafe Central',
  tenantSlug: 'cafe-central',
  amount: 10,
  currency: 'EUR',
  status: 'Pending',
  paymentMethod: 'card',
  provider: 'Mock',
  isSynthetic: true,
  createdAtUtc: '2026-08-26T10:00:00.000Z',
};

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: mockHasPermission,
  }),
}));

vi.mock('@/features/online-payments/api/onlinePaymentsApi', async () => {
  const actual = await vi.importActual<typeof import('@/features/online-payments/api/onlinePaymentsApi')>(
    '@/features/online-payments/api/onlinePaymentsApi'
  );
  return {
    ...actual,
    useOnlinePaymentsList: () => ({
      data: { items: [sample], totalCount: 1, pageNumber: 1, pageSize: 50 },
      isLoading: false,
      isFetching: false,
      isError: false,
      refetch: vi.fn(),
    }),
    useOnlinePaymentTestMutation: () => ({
      mutateAsync: vi.fn(),
      isPending: false,
    }),
  };
});

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    error: vi.fn(),
    successKey: vi.fn(),
    warning: vi.fn(),
    apiError: vi.fn(),
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    modal: { confirm: vi.fn() },
  }),
}));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return (
    <QueryClientProvider client={client}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

describe('OnlinePaymentsPage', () => {
  beforeEach(() => {
    mockHasPermission.mockReset();
  });

  it('shows a forbidden alert without online-payments.manage', () => {
    mockHasPermission.mockReturnValue(false);
    render(<OnlinePaymentsPage />, { wrapper });
    expect(screen.getByText('Sie haben keine Berechtigung für Online-Zahlungen.')).toBeInTheDocument();
    expect(screen.queryByText('Testkonsole')).not.toBeInTheDocument();
  });

  it('lists transactions and opens the test console tab', async () => {
    const user = userEvent.setup();
    mockHasPermission.mockReturnValue(true);
    render(<OnlinePaymentsPage />, { wrapper });

    expect(screen.getByRole('heading', { name: 'Online-Zahlungen' })).toBeInTheDocument();
    expect(screen.getByText('Cafe Central')).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Testkonsole' }));
    expect(screen.getByRole('button', { name: 'Testzahlung senden' })).toBeInTheDocument();
  });
});
