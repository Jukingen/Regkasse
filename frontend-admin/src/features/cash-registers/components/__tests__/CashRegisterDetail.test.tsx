import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { CashRegisterDetail } from '@/features/cash-registers/components/CashRegisterDetail';
import { I18nProvider } from '@/i18n';

const closedRegister: AdminCashRegisterListItem = {
  id: '11111111-1111-1111-1111-111111111111',
  tenantId: 'tenant-a',
  registerNumber: 'KASSE-001',
  location: 'Hauptkasse',
  status: 1,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-02-01T00:00:00Z',
};

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/cash-registers/11111111-1111-1111-1111-111111111111',
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), open: vi.fn(), destroy: vi.fn() },
    notification: { success: vi.fn(), error: vi.fn() },
    modal: { confirm: vi.fn() },
  }),
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

vi.mock('@/features/cash-registers/components/CashRegisterDetailHistory', () => ({
  CashRegisterDetailHistory: () => <div>history</div>,
}));

vi.mock('@/hooks/useCanAccessPath', () => ({
  useCanAccessPath: () => true,
}));

vi.mock('@/features/license/hooks/useLicense', () => ({
  useLicense: () => ({ licenseStatus: null }),
}));

vi.mock('@/features/cash-registers/api/cashRegisters', async () => {
  const actual = await vi.importActual<typeof import('@/features/cash-registers/api/cashRegisters')>(
    '@/features/cash-registers/api/cashRegisters'
  );
  return {
    ...actual,
    getAdminCashRegisterById: vi.fn(),
  };
});

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
  forceCloseAdminShiftRegister: vi.fn(),
}));

vi.mock('@/features/receipts/api/forensics-client', () => ({
  getReceiptListForensics: vi.fn(async () => ({
    items: [],
    page: 1,
    pageSize: 50,
    totalCount: 0,
  })),
}));

vi.mock('@/api/generated/audit-log/audit-log', () => ({
  getApiAuditLog: vi.fn(async () => ({ auditLogs: [], totalCount: 0 })),
}));

vi.mock('@/features/tenants/hooks/useTenantLimits', () => ({
  useTenantLimitUsage: () => ({ data: undefined, isLoading: false }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    isSuperAdmin: false,
    canViewCashRegisters: true,
    canManageCashRegisters: true,
    canDecommissionCashRegisters: true,
    hasPermission: () => true,
    user: { id: 'admin-1', tenantId: 'tenant-a', role: 'Manager' },
  }),
}));

import { getAdminCashRegisterById } from '@/features/cash-registers/api/cashRegisters';

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

function renderDetail(register: AdminCashRegisterListItem) {
  vi.mocked(getAdminCashRegisterById).mockResolvedValue(register);
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <CashRegisterDetail registerId={register.id} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('CashRegisterDetail', () => {
  it('shows register fields and keeps open actions enabled for a closed register', async () => {
    renderDetail(closedRegister);

    expect(await screen.findByRole('cell', { name: 'KASSE-001' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kasse öffnen' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Kasse schließen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Berichte anzeigen' })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Daten exportieren/ })).toBeEnabled();
  });

  it('disables operational actions for a decommissioned register and keeps export available', async () => {
    renderDetail({ ...closedRegister, status: 5 });

    expect(await screen.findByText('Diese Kasse wurde stillgelegt')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Kasse öffnen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Kasse schließen' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Sonderbeleg erstellen/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Berichte anzeigen' })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Daten exportieren/ })).toBeEnabled();
  });

  it('shows the open-shift cashier name next to the username', async () => {
    renderDetail({
      ...closedRegister,
      currentCashierName: 'Anna Berger',
      currentCashierUserName: 'cashier1',
      currentCashierEmail: 'anna.berger@example.com',
    });

    expect(await screen.findByText('Aktueller Kassierer')).toBeInTheDocument();
    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.getByText('(cashier1)')).toBeInTheDocument();
    expect(screen.getByText('anna.berger@example.com')).toBeInTheDocument();
  });

  it('shows empty cashier copy when the till is unattended', async () => {
    renderDetail(closedRegister);

    expect(await screen.findByText('Kein Kassierer')).toBeInTheDocument();
  });
});
