import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { fetchReceiptsByMonth, useReceiptsByMonth } from '@/features/rksv/hooks/useReceiptsByMonth';

const getApiReceiptsList = vi.fn();

vi.mock('@/api/generated/receipts/receipts', () => ({
  getApiReceiptsList: (...args: unknown[]) => getApiReceiptsList(...args),
}));

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('useReceiptsByMonth', () => {
  beforeEach(() => {
    getApiReceiptsList.mockReset();
  });

  it('fetchReceiptsByMonth passes month date range and register filter', async () => {
    getApiReceiptsList.mockResolvedValue({
      items: [{ receiptId: 'r1', receiptNumber: 'R-1', grandTotal: 12.5 }],
      totalCount: 1,
    });

    const items = await fetchReceiptsByMonth({
      cashRegisterId: 'reg-1',
      year: 2026,
      month: 3,
    });

    expect(getApiReceiptsList).toHaveBeenCalledWith({
      page: 1,
      pageSize: 100,
      sort: 'issuedAt:desc',
      cashRegisterId: 'reg-1',
      issuedFrom: '2026-03-01',
      issuedTo: '2026-03-31',
    });
    expect(items.items).toHaveLength(1);
    expect(items.items[0]?.receiptNumber).toBe('R-1');
    expect(items.totalCount).toBe(1);
    expect(items.revenue).toBe(12.5);
    expect(items.revenueIsComplete).toBe(true);
  });

  it('stays disabled when params are null', () => {
    const { result } = renderHook(() => useReceiptsByMonth(null), {
      wrapper: createWrapper(),
    });
    expect(result.current.fetchStatus).toBe('idle');
    expect(getApiReceiptsList).not.toHaveBeenCalled();
  });

  it('loads when params are complete', async () => {
    getApiReceiptsList.mockResolvedValue({ items: [] });

    const { result } = renderHook(
      () =>
        useReceiptsByMonth({
          cashRegisterId: 'reg-1',
          year: 2026,
          month: 2,
        }),
      { wrapper: createWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getApiReceiptsList).toHaveBeenCalledWith(
      expect.objectContaining({
        issuedFrom: '2026-02-01',
        issuedTo: '2026-02-28',
      })
    );
  });
});
