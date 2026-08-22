import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import { CashRegisterTable } from '@/features/cash-registers/components/CashRegisterTable';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/hooks/useCanAccessPath', () => ({
  useCanAccessPath: () => false,
}));

const mockUsePermissions = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

type AssignmentFixture = CashRegister & {
  assignedUserId?: string | null;
  assignedUserName?: string | null;
};

function registerFixture(overrides: Partial<AssignmentFixture> = {}): AssignmentFixture {
  return {
    id: 'reg-1',
    createdAt: '2026-01-01T00:00:00Z',
    registerNumber: 'KASSE-001',
    location: 'Hauptkasse',
    status: 1,
    startingBalance: 0,
    currentBalance: 0,
    lastBalanceUpdate: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

beforeAll(() => {
  global.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;

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
  mockUsePermissions.mockReturnValue({
    isSuperAdmin: false,
    hasPermission: () => false,
    user: { id: 'admin-1' },
  });
});

function renderTable(registers: AssignmentFixture[]) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterTable
          registers={registers}
          canDecommission={false}
          onEdit={vi.fn()}
          onDecommission={vi.fn()}
        />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterTable assignment column', () => {
  it('renders the assignment column header', () => {
    renderTable([registerFixture()]);

    expect(screen.getByRole('columnheader', { name: 'Zugewiesen an' })).toBeInTheDocument();
  });

  it('marks a register without assignment as unassigned', () => {
    renderTable([registerFixture({ assignedUserId: null, assignedUserName: null })]);

    expect(screen.getByText('Nicht zugewiesen')).toBeInTheDocument();
  });

  it('marks the register assigned to the signed-in admin', () => {
    renderTable([
      registerFixture({ assignedUserId: 'admin-1', assignedUserName: 'Anna Berger' }),
    ]);

    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.getByText('Mir zugewiesen')).toBeInTheDocument();
  });

  it('marks a register assigned to another cashier', () => {
    renderTable([
      registerFixture({ assignedUserId: 'cashier-9', assignedUserName: 'Bernd Huber' }),
    ]);

    expect(screen.getByText('Bernd Huber')).toBeInTheDocument();
    expect(screen.getByText('Anderem zugewiesen')).toBeInTheDocument();
  });

  it('falls back to the raw user id when the display name is missing', () => {
    renderTable([registerFixture({ assignedUserId: 'cashier-9', assignedUserName: null })]);

    expect(screen.getByText('cashier-9')).toBeInTheDocument();
  });
});
