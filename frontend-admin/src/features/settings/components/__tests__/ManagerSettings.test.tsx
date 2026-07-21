import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { ManagerSettings } from '@/features/settings/components/ManagerSettings';
import { I18nProvider } from '@/i18n';

const mockUseTenantSettings = vi.fn();
const mockUsePermissions = vi.fn();

vi.mock('@/features/settings/hooks/useTenantSettings', () => ({
  useTenantSettings: () => mockUseTenantSettings(),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({ message: { success: vi.fn(), error: vi.fn() } }),
}));

vi.mock('@/api/generated/admin/admin', () => ({
  usePutApiAdminCashRegistersId: () => ({ mutateAsync: vi.fn(), isPending: false }),
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

describe('ManagerSettings', () => {
  it('renders read-only company and fiscal sections for Manager', () => {
    mockUseTenantSettings.mockReturnValue({
      data: {
        companyName: 'Test Café',
        vatId: 'ATU12345678',
        address: 'Wien',
        taxRate: 20,
        isActive: true,
        registerId: 'reg-1',
        registerNumber: '1',
        registerLocation: 'Hauptkasse',
        tseConnected: true,
        tseStatusLabel: 'Online',
        tseType: 'device-1',
        tseSerial: null,
        certificateValidUntil: null,
      },
      isLoading: false,
      isError: false,
      error: null,
      refetch: vi.fn(),
      isFetching: false,
      registerOptions: [{ value: 'reg-1', label: '1 — Hauptkasse', register: {} }],
      selectedRegisterId: 'reg-1',
      setSelectedRegisterId: vi.fn(),
      selectedRegister: { id: 'reg-1', registerNumber: '1', location: 'Hauptkasse' },
    });
    mockUsePermissions.mockReturnValue({
      canManageCashRegisters: true,
    });

    render(
      <I18nProvider>
        <ManagerSettings />
      </I18nProvider>
    );

    expect(screen.getByText('Test Café')).toBeInTheDocument();
    expect(screen.getByText('20%')).toBeInTheDocument();
    expect(screen.getAllByText(/Nur Lese-Rechte|Read-only|Salt okunur/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /RKSV/i })).toHaveAttribute('href', '/rksv/status');
    expect(screen.getByLabelText(/Kassenname|Register name|Kasa adi/i)).toBeInTheDocument();
    expect(screen.getByText('Mein Passwort ändern')).toBeInTheDocument();
  });
});
