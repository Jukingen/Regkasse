import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { assignCashRegisterUser } from '@/features/cash-registers/api/cashRegisters';
import { CashRegisterAssignedUserField } from '@/features/cash-registers/components/CashRegisterAssignedUserField';
import { I18nProvider } from '@/i18n';

const REGISTER_ID = '11111111-1111-1111-1111-111111111111';

const notifySuccessKey = vi.fn();
const notifyApiError = vi.fn();

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ successKey: notifySuccessKey, apiError: notifyApiError }),
}));

vi.mock('@/features/users/hooks/useUsersList', () => ({
  useUsersList: () => ({
    data: {
      items: [
        { id: 'cashier-1', firstName: 'Anna', lastName: 'Berger', userName: 'cashier1' },
        { id: 'cashier-2', firstName: 'Bruno', lastName: 'Klein', userName: 'cashier2' },
      ],
    },
    isLoading: false,
  }),
}));

vi.mock('@/features/cash-registers/api/cashRegisters', async () => {
  const actual =
    await vi.importActual<typeof import('@/features/cash-registers/api/cashRegisters')>(
      '@/features/cash-registers/api/cashRegisters'
    );
  return { ...actual, assignCashRegisterUser: vi.fn() };
});

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

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(assignCashRegisterUser).mockResolvedValue({
    id: REGISTER_ID,
    tenantId: 'tenant-a',
    registerNumber: 'KASSE-001',
    location: 'Hauptkasse',
    status: 1,
  });
});

function renderField(props: Partial<React.ComponentProps<typeof CashRegisterAssignedUserField>>) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterAssignedUserField
          registerId={REGISTER_ID}
          canEdit
          assignedUserId={null}
          assignedUserName={null}
          {...props}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterAssignedUserField', () => {
  it('renders read-only text without cash_register.manage', () => {
    renderField({ canEdit: false, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Speichern' })).not.toBeInTheDocument();
  });

  it('shows the unassigned placeholder text when nobody is assigned and editing is denied', () => {
    renderField({ canEdit: false });

    expect(screen.getByText('Nicht zugewiesen')).toBeInTheDocument();
  });

  it('keeps save disabled until the selection actually changes', async () => {
    renderField({ assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled();

    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(await screen.findByTitle('Bruno Klein'));

    expect(screen.getByRole('button', { name: 'Speichern' })).toBeEnabled();
  });

  it('persists the picked cashier', async () => {
    renderField({});

    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.click(await screen.findByTitle('Anna Berger'));
    await userEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() =>
      expect(assignCashRegisterUser).toHaveBeenCalledWith(REGISTER_ID, 'cashier-1')
    );
    expect(notifySuccessKey).toHaveBeenCalledWith('cashRegisters.detail.assignedUserSaved');
  });

  it('sends null when the assignment is cleared', async () => {
    renderField({ assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    await userEvent.click(document.querySelector('.ant-select-clear') as HTMLElement);
    await userEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => expect(assignCashRegisterUser).toHaveBeenCalledWith(REGISTER_ID, null));
    expect(notifySuccessKey).toHaveBeenCalledWith('cashRegisters.detail.assignedUserCleared');
  });

  it('blocks editing for a decommissioned register', () => {
    renderField({ disabled: true, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled();
  });
});
