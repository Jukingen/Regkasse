import { describe, expect, it } from 'vitest';

import { isOpenShiftHeldBy, resolveOpenShiftHolderName } from '@/features/cash-registers/utils/shiftOccupancy';

describe('isOpenShiftHeldBy', () => {
  it('is true only when both ids are non-empty and equal', () => {
    expect(isOpenShiftHeldBy('user-1', 'user-1')).toBe(true);
    expect(isOpenShiftHeldBy(' user-1 ', 'user-1')).toBe(true);
  });

  it('is false when the till is held by someone else or unset', () => {
    expect(isOpenShiftHeldBy('cashier-2', 'admin-1')).toBe(false);
    expect(isOpenShiftHeldBy(null, 'admin-1')).toBe(false);
    expect(isOpenShiftHeldBy('cashier-2', null)).toBe(false);
    expect(isOpenShiftHeldBy('', 'admin-1')).toBe(false);
  });
});

describe('resolveOpenShiftHolderName', () => {
  it('prefers cashier display name, then username, then id', () => {
    expect(
      resolveOpenShiftHolderName({
        currentCashierName: 'Anna Berger',
        currentUser: { userName: 'cashier1' },
        currentUserId: 'u-1',
      })
    ).toBe('Anna Berger');
    expect(
      resolveOpenShiftHolderName({
        currentCashierName: null,
        currentUser: { userName: 'cashier1' },
        currentUserId: 'u-1',
      })
    ).toBe('cashier1');
    expect(resolveOpenShiftHolderName({ currentUserId: 'u-1' })).toBe('u-1');
  });
});
