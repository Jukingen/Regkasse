import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { CashRegisterOpenRequestsPanel } from '@/features/cash-registers/components/CashRegisterOpenRequestsPanel';
import { I18nProvider } from '@/i18n';

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ successKey: vi.fn(), apiError: vi.fn() }),
}));

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/features/cash-registers/hooks/useCashRegisterOpenRequests', () => ({
  useCashRegisterOpenRequests: () => ({
    data: [
      {
        id: 'req-1',
        tenantId: 't-1',
        tenantName: 'Cafe',
        tenantSlug: 'cafe',
        cashRegisterId: 'reg-1',
        registerNumber: 'K1',
        location: 'Theke',
        status: 'Pending',
        requestedByUserId: 'u-1',
        requestedByUserName: 'cashier1',
        requestedAt: '2026-09-14T10:00:00Z',
        note: null,
        resolvedByUserId: null,
        resolvedAt: null,
        resolutionNote: null,
      },
    ],
    isLoading: false,
    isError: false,
  }),
  useApproveCashRegisterOpenRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDenyCashRegisterOpenRequest: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

beforeAll(() => {
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

describe('CashRegisterOpenRequestsPanel', () => {
  it('renders pending cashier open requests', () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <I18nProvider>
          <CashRegisterOpenRequestsPanel />
        </I18nProvider>
      </QueryClientProvider>
    );
    expect(screen.getByText('K1')).toBeInTheDocument();
    expect(screen.getByText('cashier1')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Genehmigen|Approve|Onayla/i })).toBeInTheDocument();
  });
});
