import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { FinanzOnlineOutboxInbox } from '@/features/rksv-operations/components/FinanzOnlineOutboxInbox';
import { I18nProvider } from '@/i18n';

const fetchList = vi.fn();
const fetchDetail = vi.fn();
const retryMessage = vi.fn();

vi.mock('next/navigation', () => ({
  useParams: () => ({}),
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ isSuperAdmin: true }),
}));

vi.mock('@/features/rksv-operations/api/finanzOnlineOutboxUi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/features/rksv-operations/api/finanzOnlineOutboxUi')>();
  return {
    ...actual,
    fetchFinanzOnlineOutboxList: (...args: unknown[]) => fetchList(...args),
    fetchFinanzOnlineOutboxDetail: (...args: unknown[]) => fetchDetail(...args),
    retryOutboxMessage: (...args: unknown[]) => retryMessage(...args),
    retryOutboxMessages: vi.fn(),
    retryAllFailedOutbox: vi.fn(),
    markOutboxResolved: vi.fn(),
  };
});

const RAW_SIGNATURE = 'eyJhbGciOiJIUzI1NiJ9.RAW-TSE-SIGNATURE';
const RAW_PASSWORD = 'fon-secret-pin';
const OUTBOX_ID = '11111111-1111-1111-1111-111111111111';

const row = {
  outboxId: OUTBOX_ID,
  messageType: 'SessionReceipt',
  status: 'RetryableFailure',
  attemptCount: 3,
  processedAtUtc: '2026-10-12T10:00:00Z',
  lastErrorCode: 'FON-421',
  lastErrorSummary: 'Participant rejected',
  createdAtUtc: new Date().toISOString(),
  registerId: 'reg-1',
};

function renderInbox() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>
          <FinanzOnlineOutboxInbox />
        </App>
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('FinanzOnline outbox inbox', () => {
  beforeEach(() => {
    fetchList.mockReset();
    fetchDetail.mockReset();
    retryMessage.mockReset();
    fetchList.mockResolvedValue({ items: [row], total: 1 });
    fetchDetail.mockResolvedValue({
      ...row,
      payload: {
        tseSignature: RAW_SIGNATURE,
        participantCredentials: { username: 'fon-user', password: RAW_PASSWORD },
      },
    });
    retryMessage.mockResolvedValue(undefined);
  });

  it('renders the list from mocked data', async () => {
    renderInbox();
    expect(await screen.findByText('SessionReceipt')).toBeInTheDocument();
    // Layout stand-in for Playwright: /rksv/finanz-online-outbox has no e2e spec.
    // The actionable table (retry / mark-resolved) must precede the 7-day summary card.
    const table = screen.getByRole('table');
    const analytics = screen.getByTestId('fon-outbox-analytics');
    expect(table.compareDocumentPosition(analytics) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText('RetryableFailure')).toBeInTheDocument();
    expect(screen.getByText('FON-421')).toBeInTheDocument();
    expect(screen.getByText('2026-10-12T10:00:00Z')).toBeInTheDocument();
  });

  it('calls the retry API from the row action', async () => {
    renderInbox();
    const button = await screen.findByTestId(`fon-outbox-retry-${OUTBOX_ID}`);
    fireEvent.click(button);
    await waitFor(() => {
      expect(retryMessage).toHaveBeenCalledWith(OUTBOX_ID);
    });
  });

  it('shows a masked payload in the detail modal', async () => {
    renderInbox();
    fireEvent.click(await screen.findByRole('button', { name: 'Details' }));
    const payload = await screen.findByTestId('fon-outbox-payload');
    expect(payload.textContent).toContain('***');
    expect(payload.textContent).not.toContain(RAW_SIGNATURE);
    expect(payload.textContent).not.toContain(RAW_PASSWORD);
  });
});
