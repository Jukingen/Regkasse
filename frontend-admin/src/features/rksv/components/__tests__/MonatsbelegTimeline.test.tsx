import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { MonatsbelegTimeline } from '@/features/rksv/components/MonatsbelegTimeline';
import { I18nProvider } from '@/i18n';

const push = vi.fn();
const messageSuccess = vi.fn();
const messageWarning = vi.fn();
const modalConfirm = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: messageSuccess, warning: messageWarning, error: vi.fn() },
    modal: { confirm: modalConfirm },
  }),
}));

vi.mock('@/features/rksv/hooks/useReceiptsByMonth', () => ({
  useReceiptsByMonth: () => ({
    data: {
      items: [
        {
          receiptId: 'rec-1',
          receiptNumber: 'B-100',
          issuedAt: '2026-03-10T10:00:00Z',
          grandTotal: 19.9,
          rksvSpecialReceiptKind: null,
        },
      ],
      totalCount: 3,
      revenue: 19.9,
      revenueIsComplete: false,
    },
    isLoading: false,
    isFetching: false,
    isError: false,
  }),
}));

vi.mock('@/lib/clipboard', () => ({
  copyTextToClipboard: vi.fn(async () => true),
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

function renderTimeline(props: Partial<React.ComponentProps<typeof MonatsbelegTimeline>> = {}) {
  const onCreateLate = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <MonatsbelegTimeline
          year={2026}
          cashRegisterId="reg-1"
          months={[
            { month: 1, status: 'completed', receiptId: 'mb-1' },
            { month: 2, status: 'missing' },
            { month: 3, status: 'pending' },
            ...Array.from({ length: 9 }, (_, i) => ({
              month: i + 4,
              status: 'pending' as const,
            })),
          ]}
          onCreateLate={onCreateLate}
          {...props}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
  return { onCreateLate };
}

describe('MonatsbelegTimeline', () => {
  beforeEach(() => {
    push.mockReset();
    messageSuccess.mockReset();
    messageWarning.mockReset();
    modalConfirm.mockReset();
  });

  it('opens quick summary on month click with status and metrics', async () => {
    renderTimeline();

    const cards = screen.getAllByRole('button', { name: /2026/i });
    fireEvent.click(cards[2]!);

    expect(await screen.findByText(/Transaktionen/i)).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText(/Teilsumme/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Belege anzeigen/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Umsatz anzeigen/i })).toBeInTheDocument();
  });

  it('opens receipts modal from summary quick action', async () => {
    renderTimeline();

    const cards = screen.getAllByRole('button', { name: /2026/i });
    fireEvent.click(cards[2]!);
    fireEvent.click(await screen.findByRole('button', { name: /Belege anzeigen/i }));

    expect(await screen.findByText(/Belege —/i)).toBeInTheDocument();
    expect(await screen.findByText('B-100')).toBeInTheDocument();
  });

  it('navigates to payments for view-revenue from summary', async () => {
    renderTimeline();

    const cards = screen.getAllByRole('button', { name: /2026/i });
    fireEvent.click(cards[2]!);
    fireEvent.click(await screen.findByRole('button', { name: /Umsatz anzeigen/i }));

    expect(push).toHaveBeenCalledWith(
      '/payments?cashRegisterId=reg-1&startDate=2026-03-01&endDate=2026-03-31'
    );
  });

  it('calls onCreateLate for missing months from summary', async () => {
    const { onCreateLate } = renderTimeline();

    const cards = screen.getAllByRole('button', { name: /2026/i });
    fireEvent.click(cards[1]!);
    fireEvent.click(await screen.findByRole('button', { name: /Monatsbeleg erstellen/i }));

    expect(onCreateLate).toHaveBeenCalledWith(2026, 2);
  });

  it('opens recreate confirm when SuperAdmin from summary', async () => {
    renderTimeline({ canRecreate: true });

    const cards = screen.getAllByRole('button', { name: /2026/i });
    fireEvent.click(cards[0]!);
    fireEvent.click(await screen.findByRole('button', { name: /Erneut erstellen/i }));

    await waitFor(() => expect(modalConfirm).toHaveBeenCalled());
  });
});
