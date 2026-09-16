import { describe, expect, it } from '@jest/globals';

import {
  filterPaymentUsableSelectableRows,
  isOpenedOnSelect,
  isPaymentUsableSelectableRow,
  resolvePosPickerRowKind,
} from '../utils/posSelectableRegisterFilter';

describe('posSelectableRegisterFilter', () => {
  it('treats rows without status as usable (older backend without the status field)', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
      })
    ).toBe(true);
  });

  it('keeps Closed rows so the cashier can auto-open or request opening', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
        status: 'Closed',
      })
    ).toBe(true);
  });

  it('keeps Open rows when status is present', () => {
    expect(
      isPaymentUsableSelectableRow({
        id: 'a',
        registerNumber: 'K1',
        status: 'Open',
      })
    ).toBe(true);
  });

  it.each(['Decommissioned', 'Maintenance', 'Disabled', 'decommissioned'])(
    'drops %s rows (no shift can ever be opened there)',
    (status) => {
      expect(isPaymentUsableSelectableRow({ id: 'a', registerNumber: 'K1', status })).toBe(false);
    }
  );

  it('filter keeps Open and Closed but removes unusable states', () => {
    const rows = filterPaymentUsableSelectableRows([
      { id: '1', registerNumber: 'A', status: 'Closed' },
      { id: '2', registerNumber: 'B', status: 'Open' },
      { id: '3', registerNumber: 'C', status: 'Maintenance' },
      { id: '4', registerNumber: 'D', status: 'Decommissioned' },
    ]);
    expect(rows.map((r) => r.id)).toEqual(['1', '2']);
  });

  it('flags only closed rows as opened-on-select', () => {
    expect(isOpenedOnSelect({ id: '1', registerNumber: 'A', status: 'Closed' })).toBe(true);
    expect(isOpenedOnSelect({ id: '2', registerNumber: 'B', status: 'closed' })).toBe(true);
    expect(isOpenedOnSelect({ id: '3', registerNumber: 'C', status: 'Open' })).toBe(false);
    expect(isOpenedOnSelect({ id: '4', registerNumber: 'D' })).toBe(false);
  });

  it('maps picker row kind from till status and shift.open', () => {
    const closed = { id: '1', registerNumber: 'A', status: 'Closed' };
    const open = { id: '2', registerNumber: 'B', status: 'Open' };
    const maintenance = { id: '3', registerNumber: 'C', status: 'Maintenance' };
    expect(resolvePosPickerRowKind(open, true)).toBe('available');
    expect(resolvePosPickerRowKind(closed, true)).toBe('opensOnSelect');
    expect(resolvePosPickerRowKind(closed, false)).toBe('requestOpen');
    expect(resolvePosPickerRowKind(maintenance, true)).toBe('unavailable');
  });
});
