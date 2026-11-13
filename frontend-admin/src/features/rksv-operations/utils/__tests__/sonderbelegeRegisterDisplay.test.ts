import { describe, expect, it } from 'vitest';

import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import {
  compareSonderbelegeRegisters,
  formatSonderbelegeRegisterLabel,
  groupRegistersByTenant,
  isSonderbelegeRegisterTenantMismatch,
  resolveSonderbelegeStatusKey,
} from '@/features/rksv-operations/utils/sonderbelegeRegisterDisplay';

function reg(
  partial: Partial<EnhancedCashRegister> &
    Pick<EnhancedCashRegister, 'id' | 'tenantId' | 'registerNumber'>
): EnhancedCashRegister {
  return {
    location: '',
    status: 1,
    ...partial,
  } as EnhancedCashRegister;
}

describe('sonderbelegeRegisterDisplay', () => {
  it('formats register number, name and tenant', () => {
    expect(
      formatSonderbelegeRegisterLabel(
        reg({
          id: '1',
          tenantId: 't1',
          registerNumber: '001',
          location: 'Hauptkasse',
          tenantName: 'Dev Tenant',
        })
      )
    ).toBe('001 - Hauptkasse (Dev Tenant)');
  });

  it('maps open status to active', () => {
    expect(resolveSonderbelegeStatusKey(2)).toBe('active');
    expect(resolveSonderbelegeStatusKey(5)).toBe('decommissioned');
    expect(resolveSonderbelegeStatusKey(1)).toBe('closed');
    expect(resolveSonderbelegeStatusKey(3)).toBe('maintenance');
  });

  it('sorts active registers first then by tenant and number', () => {
    const a = reg({
      id: 'a',
      tenantId: 't2',
      tenantName: 'Beta',
      registerNumber: '002',
      status: 2,
    });
    const b = reg({
      id: 'b',
      tenantId: 't1',
      tenantName: 'Alpha',
      registerNumber: '001',
      status: 1,
    });
    const c = reg({
      id: 'c',
      tenantId: 't1',
      tenantName: 'Alpha',
      registerNumber: '010',
      status: 2,
    });
    const sorted = [b, a, c].sort(compareSonderbelegeRegisters);
    expect(sorted.map((r) => r.id)).toEqual(['c', 'a', 'b']);
  });

  it('groups registers by tenant with counts', () => {
    const groups = groupRegistersByTenant([
      reg({
        id: '1',
        tenantId: 't1',
        tenantName: 'Dev',
        registerNumber: '002',
        status: 1,
      }),
      reg({
        id: '2',
        tenantId: 't1',
        tenantName: 'Dev',
        registerNumber: '001',
        status: 2,
      }),
      reg({
        id: '3',
        tenantId: 't2',
        tenantName: 'Prod',
        registerNumber: '001',
        status: 5,
      }),
    ]);
    expect(groups).toHaveLength(2);
    expect(groups[0]?.tenantName).toBe('Dev');
    expect(groups[0]?.registers.map((r) => r.registerNumber)).toEqual(['001', '002']);
    expect(groups[1]?.registers).toHaveLength(1);
  });

  it('detects register vs ambient tenant mismatch', () => {
    const register = reg({ id: '1', tenantId: 'Tenant-A', registerNumber: '001' });
    expect(isSonderbelegeRegisterTenantMismatch(register, 'tenant-a')).toBe(false);
    expect(isSonderbelegeRegisterTenantMismatch(register, 'tenant-b')).toBe(true);
    expect(isSonderbelegeRegisterTenantMismatch(register, undefined)).toBe(false);
    expect(isSonderbelegeRegisterTenantMismatch(undefined, 'tenant-a')).toBe(false);
  });
});
