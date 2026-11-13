import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import { SonderbelegeRegisterPicker } from '@/features/rksv-operations/components/SonderbelegeRegisterPicker';
import { I18nProvider } from '@/i18n';

const ambientTenant = 'tenant-dev';

function reg(
  partial: Partial<EnhancedCashRegister> & Pick<EnhancedCashRegister, 'id' | 'tenantId' | 'registerNumber'>
): EnhancedCashRegister {
  return {
    location: 'Theke',
    status: 1,
    tenantName: 'Dev Tenant',
    tenantSlug: 'dev',
    ...partial,
  } as EnhancedCashRegister;
}

function renderPicker(props: Partial<ComponentProps<typeof SonderbelegeRegisterPicker>> = {}) {
  return render(
    <I18nProvider>
      <SonderbelegeRegisterPicker
        registers={[
          reg({
            id: 'reg-1',
            tenantId: ambientTenant,
            registerNumber: '001',
            tenantName: 'Dev Tenant',
          }),
        ]}
        registerId="reg-1"
        onRegisterChange={vi.fn()}
        tenants={[{ id: ambientTenant, name: 'Dev Tenant', slug: 'dev' }]}
        ambientTenantId={ambientTenant}
        {...props}
      />
    </I18nProvider>
  );
}

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

describe('SonderbelegeRegisterPicker', () => {
  it('shows a tenant badge next to the register name', () => {
    renderPicker();
    expect(screen.getAllByText('Dev Tenant').length).toBeGreaterThan(0);
    expect(screen.getByText('Theke')).toBeInTheDocument();
  });

  it('does not call onRegisterChange when the selected register belongs to another tenant', async () => {
    const onRegisterChange = vi.fn();
    const onRegisterTenantMismatch = vi.fn();
    const other = reg({
      id: 'reg-other',
      tenantId: 'tenant-cafe',
      registerNumber: '002',
      location: 'Bar',
      tenantName: 'Cafe',
      tenantSlug: 'cafe',
    });

    renderPicker({
      registers: [
        reg({
          id: 'reg-1',
          tenantId: ambientTenant,
          registerNumber: '001',
          tenantName: 'Dev Tenant',
        }),
        other,
      ],
      registerId: undefined,
      onRegisterChange,
      onRegisterTenantMismatch,
    });

    await userEvent.click(screen.getByText('Bar'));
    expect(onRegisterTenantMismatch).toHaveBeenCalledWith(expect.objectContaining({ id: 'reg-other' }));
    expect(onRegisterChange).not.toHaveBeenCalled();
  });
});
