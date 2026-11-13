import { describe, expect, it } from 'vitest';

import {
  cashRegisterDetailPath,
  cashRegisterReportsPath,
  isCashRegisterDetailPath,
  parseCashRegisterDetailId,
} from '@/shared/cashRegisterRoutes';

describe('cashRegisterRoutes', () => {
  it('builds detail and reports paths', () => {
    expect(cashRegisterDetailPath('  abc  ')).toBe('/admin/cash-registers/abc');
    expect(cashRegisterReportsPath('reg-1')).toBe('/admin/reports?registerId=reg-1');
    expect(cashRegisterReportsPath()).toBe('/admin/reports');
  });

  it('parses UUID detail paths only', () => {
    const id = '11111111-1111-1111-1111-111111111111';
    expect(isCashRegisterDetailPath(`/admin/cash-registers/${id}`)).toBe(true);
    expect(parseCashRegisterDetailId(`/admin/cash-registers/${id}/`)).toBe(id);
    expect(isCashRegisterDetailPath('/admin/cash-registers')).toBe(false);
    expect(isCashRegisterDetailPath('/admin/cash-registers/not-a-guid')).toBe(false);
  });
});
