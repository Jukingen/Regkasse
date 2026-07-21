import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import { CashRegisterGrid } from '@/features/cash-registers/components/CashRegisterGrid';
import { I18nProvider } from '@/i18n';

const sampleRegister: CashRegister = {
  id: 'reg-1',
  createdAt: '2026-01-01T00:00:00Z',
  registerNumber: 'KASSE-001',
  location: 'Hauptkasse',
  status: 1,
  startingBalance: 10,
  currentBalance: 42,
  lastBalanceUpdate: '2026-01-01T00:00:00Z',
  currentUserId: 'user-1',
};

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

function renderGrid(props: Partial<React.ComponentProps<typeof CashRegisterGrid>> = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterGrid
          registers={[sampleRegister]}
          canDecommission
          canManage
          statusLabel={() => 'Geschlossen'}
          onEdit={vi.fn()}
          onDecommission={vi.fn()}
          {...props}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterGrid', () => {
  it('renders register content and actions', () => {
    renderGrid();

    expect(screen.getByText('KASSE-001')).toBeInTheDocument();
    expect(screen.getByText('Hauptkasse')).toBeInTheDocument();
    expect(screen.getByText('Geschlossen')).toBeInTheDocument();
    expect(screen.getByLabelText('Details')).toBeInTheDocument();
    expect(screen.getByLabelText('Stilllegen')).toBeInTheDocument();
    expect(screen.getByLabelText('Sonderbelege')).toBeInTheDocument();
  });

  it('shows empty state when there are no registers', () => {
    renderGrid({ registers: [], totalRegisterCount: 0 });

    expect(screen.getByText(/Keine Kassen vorhanden/i)).toBeInTheDocument();
  });
});
