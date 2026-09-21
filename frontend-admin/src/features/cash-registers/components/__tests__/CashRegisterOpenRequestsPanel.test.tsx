import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { CashRegisterOpenRequestsPanel } from '@/features/cash-registers/components/CashRegisterOpenRequestsPanel';
import { I18nProvider } from '@/i18n';

const pendingRow = {
  id: 'req-1',
  tenantId: 't-1',
  tenantName: 'Cafe',
  tenantSlug: 'cafe',
  cashRegisterId: 'reg-1',
  registerNumber: 'K1',
  location: 'Theke',
  status: 'Pending' as const,
  requestedByUserId: 'u-1',
  requestedByUserName: 'cashier1',
  requestedAt: '2026-09-14T10:00:00Z',
  note: null,
  resolvedByUserId: null,
  resolvedAt: null,
  resolutionNote: null,
};

const { successKey, approveMutate, denyMutate, listState, lastConfirm } = vi.hoisted(() => ({
  successKey: vi.fn(),
  approveMutate: vi.fn(),
  denyMutate: vi.fn(),
  listState: {
    data: [] as Array<typeof pendingRow>,
    isLoading: false,
    isError: false,
    isRefetching: false,
    refetch: vi.fn(),
  },
  lastConfirm: {
    current: null as null | {
      onOk?: () => Promise<void>;
      content?: React.ReactNode;
    },
  },
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ successKey, apiError: vi.fn() }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    modal: {
      confirm: (config: { onOk?: () => Promise<void>; content?: React.ReactNode }) => {
        lastConfirm.current = config;
      },
    },
  }),
}));

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/features/cash-registers/hooks/useCashRegisterOpenRequests', () => ({
  useCashRegisterOpenRequests: () => listState,
  useApproveCashRegisterOpenRequest: () => ({ mutateAsync: approveMutate, isPending: false }),
  useDenyCashRegisterOpenRequest: () => ({ mutateAsync: denyMutate, isPending: false }),
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

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterOpenRequestsPanel />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterOpenRequestsPanel', () => {
  beforeEach(() => {
    successKey.mockReset();
    approveMutate.mockReset();
    denyMutate.mockReset();
    approveMutate.mockResolvedValue(pendingRow);
    denyMutate.mockResolvedValue(pendingRow);
    lastConfirm.current = null;
    listState.data = [pendingRow];
    listState.isLoading = false;
    listState.isError = false;
    listState.isRefetching = false;
  });

  it('approves a pending request and shows the success toast', async () => {
    renderPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Genehmigen' }));
    await waitFor(() => {
      expect(approveMutate).toHaveBeenCalledWith({ id: 'req-1' });
      expect(successKey).toHaveBeenCalledWith('cashRegisters.openRequests.approved');
    });
  });

  it('denies with an empty note as id only', async () => {
    renderPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Ablehnen' }));
    await lastConfirm.current?.onOk?.();
    expect(denyMutate).toHaveBeenCalledWith({ id: 'req-1' });
  });

  it('denies with a trimmed note', async () => {
    renderPanel();
    fireEvent.click(screen.getByRole('button', { name: 'Ablehnen' }));
    render(<>{lastConfirm.current?.content}</>);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: '  Schicht schon offen  ' } });
    await lastConfirm.current?.onOk?.();
    expect(denyMutate).toHaveBeenCalledWith({ id: 'req-1', note: 'Schicht schon offen' });
  });

  it('shows the empty state when there are no pending requests', () => {
    listState.data = [];
    renderPanel();
    expect(screen.getByText('Keine offenen Anfragen.')).toBeInTheDocument();
  });
});
