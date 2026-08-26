import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { AdminOnlinePaymentDto } from '@/features/online-payments/api/onlinePaymentsApi';
import { OnlinePaymentTestConsole } from '@/features/online-payments/components/OnlinePaymentTestConsole';
import { I18nProvider } from '@/i18n';

const mockMutateAsync = vi.fn();
const mockNotifyError = vi.fn();
const mockNotifySuccessKey = vi.fn();
const mockNotifyWarning = vi.fn();
const mockNotifyApiError = vi.fn();
const mockModalConfirm = vi.fn();

vi.mock('@/features/online-payments/api/onlinePaymentsApi', async () => {
  const actual = await vi.importActual<typeof import('@/features/online-payments/api/onlinePaymentsApi')>(
    '@/features/online-payments/api/onlinePaymentsApi'
  );
  return {
    ...actual,
    useOnlinePaymentTestMutation: () => ({
      mutateAsync: mockMutateAsync,
      isPending: false,
    }),
  };
});

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    error: mockNotifyError,
    successKey: mockNotifySuccessKey,
    warning: mockNotifyWarning,
    apiError: mockNotifyApiError,
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    modal: {
      confirm: (opts: { onOk?: () => Promise<void> | void }) => {
        mockModalConfirm(opts);
        return opts.onOk?.();
      },
    },
  }),
}));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={client}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

function payment(overrides: Partial<AdminOnlinePaymentDto> = {}): AdminOnlinePaymentDto {
  return {
    id: 'op-1',
    tenantId: 'tenant-1',
    tenantName: 'Cafe Central',
    tenantSlug: 'cafe-central',
    amount: 10,
    currency: 'EUR',
    status: 'Pending',
    paymentMethod: 'card',
    provider: 'Mock',
    paymentIntentId: 'pi_test',
    isSynthetic: true,
    createdAtUtc: '2026-08-26T10:00:00.000Z',
    ...overrides,
  };
}

describe('OnlinePaymentTestConsole', () => {
  beforeEach(() => {
    mockMutateAsync.mockReset();
    mockNotifyError.mockReset();
    mockNotifySuccessKey.mockReset();
    mockNotifyWarning.mockReset();
    mockNotifyApiError.mockReset();
    mockModalConfirm.mockReset();
  });

  it('sends a synthetic test payment and enables webhook buttons', async () => {
    const user = userEvent.setup();
    const created = payment();
    mockMutateAsync.mockResolvedValue({ succeeded: true, transaction: created });
    const onPaymentCreated = vi.fn();

    render(<OnlinePaymentTestConsole onPaymentCreated={onPaymentCreated} />, { wrapper });

    expect(screen.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Fehlgeschlagenen Webhook simulieren' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Testzahlung senden' }));

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenCalledWith({
        action: 'create',
        amount: 10,
        paymentMethod: 'card',
      });
    });
    expect(onPaymentCreated).toHaveBeenCalledWith(created);
    expect(mockNotifySuccessKey).toHaveBeenCalledWith('onlinePayments.console.createSuccess');
    expect(screen.getByText(/Letzte Testzahlung: op-1/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' })).toBeEnabled();
  });

  it('simulates a successful webhook for the created payment', async () => {
    const user = userEvent.setup();
    mockMutateAsync
      .mockResolvedValueOnce({ succeeded: true, transaction: payment() })
      .mockResolvedValueOnce({
        succeeded: true,
        transaction: payment({ status: 'Succeeded', lastWebhookEvent: 'payment_intent.succeeded' }),
      });

    render(<OnlinePaymentTestConsole />, { wrapper });
    await user.click(screen.getByRole('button', { name: 'Testzahlung senden' }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' })).toBeEnabled());

    await user.click(screen.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' }));

    await waitFor(() => {
      expect(mockMutateAsync).toHaveBeenLastCalledWith({
        action: 'webhookSucceeded',
        transactionId: 'op-1',
      });
    });
    expect(mockNotifySuccessKey).toHaveBeenCalledWith('onlinePayments.console.webhookSuccessToast');
  });

  it('keeps webhook simulation disabled until a test payment exists', () => {
    render(<OnlinePaymentTestConsole />, { wrapper });

    expect(screen.getByRole('button', { name: 'Erfolgreichen Webhook simulieren' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Fehlgeschlagenen Webhook simulieren' })).toBeDisabled();
    expect(mockMutateAsync).not.toHaveBeenCalled();
  });
});
