import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import { CashRegisterDetailDrawer } from '@/features/cash-registers/components/CashRegisterDetailDrawer';
import { useCashRegisterPermissions } from '@/features/cash-registers/hooks/useCashRegisterPermissions';
import { I18nProvider } from '@/i18n';

vi.mock('@/hooks/useCanAccessPath', () => ({
  useCanAccessPath: () => true,
}));

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/features/tenants/hooks/useTenantLimits', () => ({
  useTenantLimitUsage: () => ({ data: undefined, isLoading: false }),
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

function renderDrawer(register: CashRegister & { assignedUserName?: string | null }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterDetailDrawer open register={register} onClose={vi.fn()} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterDetailDrawer', () => {
  it('keeps Sonderbeleg actions enabled for a closed register', () => {
    renderDrawer(sampleRegister);

    expect(screen.getByRole('link', { name: 'Startbeleg' })).toBeEnabled();
    expect(screen.getByRole('link', { name: 'Monatsbeleg' })).toBeEnabled();
    expect(screen.getByRole('link', { name: 'Jahresbeleg' })).toBeEnabled();
    expect(screen.getByRole('link', { name: 'Schlussbeleg (Endbeleg)' })).toBeEnabled();
  });

  it('disables Sonderbeleg actions for a decommissioned register', () => {
    renderDrawer({ ...sampleRegister, status: 5 });

    expect(screen.getByRole('button', { name: 'Startbeleg' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Monatsbeleg' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Jahresbeleg' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Schlussbeleg (Endbeleg)' })).toBeDisabled();
    expect(
      screen.getByText('Diese Kasse wurde stillgelegt')
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        'Diese Kasse kann nicht mehr geöffnet werden. Alle historischen Daten sind weiterhin einsehbar.'
      )
    ).toBeInTheDocument();
  });

  it('shows the assignment read-only without cash_register.manage', () => {
    renderDrawer({ ...sampleRegister, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Speichern' })).not.toBeInTheDocument();
  });

  it('lets SuperAdmin/Mandanten-Admin edit the assignment', () => {
    setPermissions({ canAssignUser: true });
    renderDrawer({ ...sampleRegister, assignedUserId: 'cashier-1', assignedUserName: 'Anna Berger' });

    expect(screen.getByRole('button', { name: 'Speichern' })).toBeInTheDocument();
  });
});
