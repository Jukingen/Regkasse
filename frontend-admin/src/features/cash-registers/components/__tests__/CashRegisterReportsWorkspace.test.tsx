import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { CashRegisterReportsWorkspace } from '@/features/cash-registers/components/CashRegisterReportsWorkspace';
import { I18nProvider } from '@/i18n';

const closedRegister: AdminCashRegisterListItem = {
  id: '11111111-1111-1111-1111-111111111111',
  tenantId: 'tenant-a',
  registerNumber: 'KASSE-001',
  location: 'Hauptkasse',
  status: 1,
};

let selectedRegister: AdminCashRegisterListItem = closedRegister;

vi.mock('@/hooks/useCashRegisterSelection', () => ({
  useCashRegisterSelection: () => ({
    selectedRegister,
    selectedRegisterId: selectedRegister.id,
    registers: [selectedRegister],
    isLoading: false,
    error: null,
    setSelectedRegisterId: vi.fn(),
    registerOptions: [],
  }),
}));

vi.mock('@/components/CashRegisterSelector', () => ({
  CashRegisterSelector: () => <div>selector</div>,
}));

vi.mock('@/features/payments/api/adminPaymentsListQuery', () => ({
  fetchAdminPaymentsList: vi.fn(async () => ({
    items: [],
    total: 0,
    page: 1,
    pageSize: 50,
    hasMore: false,
  })),
}));

vi.mock('@/features/shifts/api/shiftsOverview', () => ({
  fetchAdminShiftOverview: vi.fn(async () => ({
    activeShifts: [],
    shiftHistory: [],
    dailyClosings: [],
  })),
}));

vi.mock('@/features/receipts/api/forensics-client', () => ({
  getReceiptListForensics: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 50,
    totalCount: 0,
  })),
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

function renderReports(register: AdminCashRegisterListItem) {
  selectedRegister = register;
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterReportsWorkspace initialRegisterId={register.id} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterReportsWorkspace', () => {
  it('shows report types and keeps export plus operational actions available for a closed register', async () => {
    renderReports(closedRegister);

    expect((await screen.findAllByText('Umsatzbericht')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('Transaktionsbericht').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Schichtbericht').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Sonderbelegbericht').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Bericht exportieren/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Daten exportieren/ })).toBeEnabled();
    expect(screen.getByRole('link', { name: 'Kasse öffnen' })).toHaveAttribute(
      'href',
      `/admin/cash-registers/${closedRegister.id}`
    );
    expect(screen.getByRole('link', { name: 'Schicht öffnen' })).toHaveAttribute(
      'href',
      `/admin/cash-registers/${closedRegister.id}`
    );
  });

  it('disables operational actions for a decommissioned register and keeps export available', async () => {
    renderReports({ ...closedRegister, status: 5 });

    expect(await screen.findByText('Diese Kasse wurde stillgelegt')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kasse öffnen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Schicht öffnen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Sonderbeleg erstellen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Bericht exportieren/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Daten exportieren/ })).toBeEnabled();
  });
});
