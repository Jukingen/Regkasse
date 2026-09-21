import { describe, expect, it } from '@jest/globals';

import {
  filterAssignedToUser,
  filterPaymentUsableSelectableRows,
  isOpenedOnSelect,
  isPaymentUsableSelectableRow,
  resolvePosPickerRowKind,
  sortSelectableRegisters,
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

  it('sorts Open before Closed', () => {
    const sorted = sortSelectableRegisters([
      { id: 'closed', registerNumber: '1', status: 'Closed' },
      { id: 'open', registerNumber: '9', status: 'Open' },
    ]);
    expect(sorted.map((row) => row.id)).toEqual(['open', 'closed']);
  });

  it('sorts registerNumber within each group so 2 comes before 10', () => {
    const sorted = sortSelectableRegisters([
      { id: 'open-10', registerNumber: '10', status: 'Open' },
      { id: 'closed-10', registerNumber: '10', status: 'Closed' },
      { id: 'open-2', registerNumber: '2', status: 'Open' },
      { id: 'closed-2', registerNumber: '2', status: 'Closed' },
    ]);
    expect(sorted.map((row) => row.id)).toEqual(['open-2', 'open-10', 'closed-2', 'closed-10']);
  });

  it('keeps original order when register numbers are equal', () => {
    const sorted = sortSelectableRegisters([
      { id: 'second', registerNumber: '1', status: 'Open' },
      { id: 'first', registerNumber: '1', status: ' open ' },
    ]);
    expect(sorted.map((row) => row.id)).toEqual(['second', 'first']);
  });

  it('keeps rows assigned to the user and drops shared rows', () => {
    const rows = filterAssignedToUser(
      [
        { id: 'mine', registerNumber: '1', assignedUserId: 'cashier-1' },
        { id: 'shared', registerNumber: '2', assignedUserId: null },
      ],
      'cashier-1'
    );
    expect(rows.map((row) => row.id)).toEqual(['mine']);
  });

  it('drops a register assigned to another user', () => {
    const rows = filterAssignedToUser(
      [{ id: 'other', registerNumber: '1', assignedUserId: 'cashier-2' }],
      'cashier-1'
    );
    expect(rows).toEqual([]);
  });

  it('returns an empty list for an empty input or an empty user id', () => {
    expect(filterAssignedToUser([], 'cashier-1')).toEqual([]);
    expect(
      filterAssignedToUser(
        [{ id: 'mine', registerNumber: '1', assignedUserId: 'cashier-1' }],
        '   '
      )
    ).toEqual([]);
  });
});
