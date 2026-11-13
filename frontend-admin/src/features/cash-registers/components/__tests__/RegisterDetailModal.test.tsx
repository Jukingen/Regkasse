import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import { RegisterDetailModal } from '@/features/cash-registers/components/RegisterDetailModal';
import { useCashRegisterPermissions } from '@/features/cash-registers/hooks/useCashRegisterPermissions';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/features/cash-registers/hooks/useCashRegisterPermissions', () => ({
  useCashRegisterPermissions: vi.fn(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ successKey: vi.fn(), apiError: vi.fn() }),
}));

vi.mock('@/features/users/hooks/useUsersList', () => ({
  useUsersList: () => ({ data: { items: [] }, isLoading: false }),
}));

function setPermissions(overrides: { canAssignUser?: boolean } = {}) {
  vi.mocked(useCashRegisterPermissions).mockReturnValue({
    canView: true,
    canEdit: false,
    canAssignUser: false,
    canOpen: false,
    canClose: false,
    canManageShifts: false,
    canViewReports: false,
    canExport: false,
    isDecommissioned: false,
    ...overrides,
  });
}

const sampleRegister: CashRegister = {
  id: '11111111-1111-1111-1111-111111111111',
  createdAt: '2026-01-01T00:00:00Z',
  registerNumber: 'KASSE-001',
  location: 'Hauptkasse',
  status: 1,
  startingBalance: 0,
  currentBalance: 0,
  lastBalanceUpdate: '2026-01-01T00:00:00Z',
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

beforeEach(() => {
  vi.clearAllMocks();
  setPermissions();
});

function renderModal(register: CashRegister & { assignedUserName?: string | null }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <RegisterDetailModal open register={register} onClose={vi.fn()} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('RegisterDetailModal assignment', () => {
  it('shows the assignment read-only without cash_register.manage', () => {
    renderModal({ ...sampleRegister, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByText('Zugewiesener Kassierer')).toBeInTheDocument();
    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Speichern' })).not.toBeInTheDocument();
  });

  it('lets SuperAdmin edit the assignment', () => {
    setPermissions({ canAssignUser: true });
    renderModal({ ...sampleRegister, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByRole('button', { name: 'Speichern' })).toBeInTheDocument();
  });

  it('disables assignment editing for a decommissioned register', () => {
    setPermissions({ canAssignUser: true });
    renderModal({
      ...sampleRegister,
      status: 5,
      assignedUserId: 'cashier-1',
      assignedUserName: 'Anna Berger',
    });

    expect(screen.getByRole('combobox', { name: 'Zugewiesener Kassierer' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Speichern' })).toBeDisabled();
  });
});
