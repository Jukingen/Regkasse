/**
 * Voucher detail journal: all ledger lines from the API must render as table rows.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, within } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminVoucherLedgerLineDto } from '@/api/admin/vouchers';
import { VoucherHistory } from '@/features/vouchers/components/VoucherHistory';
import { I18nProvider } from '@/i18n';

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

const lines: AdminVoucherLedgerLineDto[] = [
  {
    id: '1',
    type: 'Issue',
    amount: 50,
    balanceAfter: 50,
    paymentId: null,
    receiptId: null,
    receiptNumber: null,
    createdByUserId: 'u-admin',
    createdByDisplayName: 'Admin',
    createdByEmail: null,
    createdByRoles: ['Manager'],
    createdAtUtc: '2026-05-01T08:00:00Z',
    correlationId: null,
  },
  {
    id: '2',
    type: 'Redeem',
    amount: -10,
    balanceAfter: 40,
    paymentId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    receiptId: null,
    receiptNumber: 'AT-K1-20260501-1',
    createdByUserId: 'u-cashier',
    createdByDisplayName: 'Kassierer',
    createdByEmail: null,
    createdByRoles: ['Cashier'],
    createdAtUtc: '2026-05-02T09:00:00Z',
    correlationId: null,
  },
  {
    id: '3',
    type: 'Redeem',
    amount: -5,
    balanceAfter: 35,
    paymentId: 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff',
    receiptId: null,
    receiptNumber: 'AT-K1-20260502-2',
    createdByUserId: 'u-cashier',
    createdByDisplayName: 'Kassierer',
    createdByEmail: null,
    createdByRoles: ['Cashier'],
    createdAtUtc: '2026-05-03T10:00:00Z',
    correlationId: null,
  },
];

vi.mock('@/api/admin/vouchers', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/admin/vouchers')>();
  return {
    ...actual,
    useAdminVoucherLedger: vi.fn(() => ({
      data: lines,
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
    })),
  };
});

function renderWithProviders(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>{ui}</I18nProvider>
    </QueryClientProvider>
  );
}

describe('VoucherHistory', () => {
  it('renders one table row per ledger line', async () => {
    renderWithProviders(<VoucherHistory voucherId="v1" ledgerEnabled currency="EUR" />);

    const tables = await screen.findAllByRole('table');
    expect(tables.length).toBeGreaterThan(0);
    const mainTable = tables[0]!;
    const bodyRows = within(mainTable).getAllByRole('row').slice(1);
    expect(bodyRows).toHaveLength(lines.length);
  });
});
