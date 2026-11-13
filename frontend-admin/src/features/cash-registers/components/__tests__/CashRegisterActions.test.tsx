import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import type { CashRegister } from '@/api/generated/model';
import {
  type CashRegisterActionKey,
  CashRegisterActions,
} from '@/features/cash-registers/components/CashRegisterActions';
import { I18nProvider } from '@/i18n';

const mockUsePermissions = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

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
  mockUsePermissions.mockReturnValue({
    isSuperAdmin: false,
    canManageCashRegisters: true,
    canDecommissionCashRegisters: false,
    hasPermission: () => false,
    user: { id: 'admin-1', tenantId: 'tenant-a', role: 'Manager' },
  });
});

function renderActions(props: Partial<React.ComponentProps<typeof CashRegisterActions>> = {}) {
  const onAction = vi.fn<(key: CashRegisterActionKey, register: CashRegister) => void>();
  render(
    <I18nProvider>
      <CashRegisterActions register={sampleRegister} onAction={onAction} {...props} />
    </I18nProvider>
  );
  return { onAction };
}

describe('CashRegisterActions', () => {
  it('shows shift and daily closing actions for Manager without decommission', () => {
    renderActions();

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Schicht öffnen')).toBeInTheDocument();
    expect(screen.getByText('Schicht schließen')).toBeInTheDocument();
    expect(screen.getByText('Tagesabschluss')).toBeInTheDocument();
    expect(screen.getByText('Bearbeiten')).toBeInTheDocument();
    expect(screen.queryByText('Stilllegen')).not.toBeInTheDocument();
    expect(screen.queryByText('Löschen')).not.toBeInTheDocument();
  });

  it('shows decommission for Mandanten-Admin with cash_register.decommission', () => {
    mockUsePermissions.mockReturnValue({
      isSuperAdmin: false,
      canManageCashRegisters: true,
      canDecommissionCashRegisters: true,
      hasPermission: () => false,
      user: { id: 'admin-1', tenantId: 'tenant-a', role: 'Manager' },
    });
    renderActions();

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Stilllegen')).toBeInTheDocument();
    expect(screen.queryByText('Löschen')).not.toBeInTheDocument();
  });

  it('shows lifecycle actions including hard delete for Super Admin', () => {
    mockUsePermissions.mockReturnValue({
      isSuperAdmin: true,
      canManageCashRegisters: true,
      canDecommissionCashRegisters: true,
      hasPermission: () => true,
      user: { id: 'root', tenantId: 'tenant-a', role: 'SuperAdmin' },
    });
    renderActions();

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Bearbeiten')).toBeInTheDocument();
    expect(screen.getByText('Löschen')).toBeInTheDocument();
    expect(screen.getByText('Stilllegen')).toBeInTheDocument();
  });

  it('calls onAction when a menu item is selected', () => {
    const { onAction } = renderActions();

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));
    fireEvent.click(screen.getByText('Tagesabschluss'));

    expect(onAction).toHaveBeenCalledWith('daily-closing', sampleRegister);
  });

  it('disables close when the open shift belongs to another user', () => {
    renderActions({
      register: {
        ...sampleRegister,
        status: 2,
        currentUserId: 'cashier-9',
      },
    });

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Schicht schließen').closest('li')).toHaveAttribute('aria-disabled', 'true');
  });

  it('enables close for Manager with shift.manage even when another user holds the till', () => {
    mockUsePermissions.mockReturnValue({
      isSuperAdmin: false,
      canManageCashRegisters: true,
      canDecommissionCashRegisters: false,
      hasPermission: (permission: string) => permission === 'shift.manage',
      user: { id: 'admin-1', tenantId: 'tenant-a', role: 'Manager' },
    });

    renderActions({
      register: {
        ...sampleRegister,
        status: 2,
        currentUserId: 'cashier-9',
      },
    });

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Schicht schließen').closest('li')).not.toHaveAttribute(
      'aria-disabled',
      'true'
    );
  });

  it('enables close when the signed-in admin holds the open shift', () => {
    renderActions({
      register: {
        ...sampleRegister,
        status: 2,
        currentUserId: 'admin-1',
      },
    });

    fireEvent.click(screen.getByRole('button', { name: /Aktionen/i }));

    expect(screen.getByText('Schicht schließen').closest('li')).not.toHaveAttribute(
      'aria-disabled',
      'true'
    );
  });

  it('disables Kasse öffnen for a decommissioned register', () => {
    renderActions({
      register: { ...sampleRegister, status: 5 },
    });

    const openButton = screen.getByRole('button', { name: /Kasse öffnen/i });
    expect(openButton).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Aktionen/i })).not.toBeInTheDocument();
    expect(screen.queryByText('Schicht öffnen')).not.toBeInTheDocument();
  });
});
