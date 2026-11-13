import { describe, expect, it } from '@jest/globals';

import {
  filterSelectableRegistersForPosUser,
  isSelectableRegisterVisibleToUser,
  seesEveryPosRegisterAssignment,
} from '../utils/posRegisterAssignmentVisibility';
import { pickFirstFreeTableNumber } from '../utils/posTableOrder';

const mine = { id: 'k1', registerNumber: 'K1', assignedUserId: 'u1' };
const shared = { id: 'k2', registerNumber: 'K2', assignedUserId: null };
const theirs = { id: 'k3', registerNumber: 'K3', assignedUserId: 'u2' };
const legacy = { id: 'k4', registerNumber: 'K4' };

describe('posRegisterAssignmentVisibility', () => {
  it('treats missing/null assignment as shared', () => {
    expect(isSelectableRegisterVisibleToUser('u1', null)).toBe(true);
    expect(isSelectableRegisterVisibleToUser('u1', '')).toBe(true);
    expect(isSelectableRegisterVisibleToUser('u1', undefined)).toBe(true);
  });

  it('hides registers assigned to another user', () => {
    expect(isSelectableRegisterVisibleToUser('u1', 'u2')).toBe(false);
    expect(isSelectableRegisterVisibleToUser('u1', 'u1')).toBe(true);
  });

  it('SuperAdmin and Manager see every assignment', () => {
    expect(seesEveryPosRegisterAssignment({ role: 'SuperAdmin' })).toBe(true);
    expect(seesEveryPosRegisterAssignment({ role: 'Cashier', roles: ['Manager'] })).toBe(true);
    expect(seesEveryPosRegisterAssignment({ role: 'Cashier' })).toBe(false);
    expect(seesEveryPosRegisterAssignment({ role: 'Waiter' })).toBe(false);
  });

  it('filters Cashier/Waiter pick lists to assigned-or-shared', () => {
    const rows = [mine, shared, theirs, legacy];
    expect(filterSelectableRegistersForPosUser(rows, { id: 'u1', role: 'Cashier' }).map((r) => r.id)).toEqual([
      'k1',
      'k2',
      'k4',
    ]);
    expect(filterSelectableRegistersForPosUser(rows, { id: 'u1', role: 'Waiter' }).map((r) => r.id)).toEqual([
      'k1',
      'k2',
      'k4',
    ]);
    expect(
      filterSelectableRegistersForPosUser(rows, { id: 'u1', role: 'SuperAdmin' }).map((r) => r.id)
    ).toEqual(['k1', 'k2', 'k3', 'k4']);
  });
});

const POS_FULL: ReadonlyArray<readonly [number, number]> = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(
  (n) => [n, 1] as const
);

describe('pickFirstFreeTableNumber', () => {
  it('returns the first table 1–10 with zero items', () => {
    const counts = new Map<number, number>([
      [1, 2],
      [2, 1],
    ]);
    expect(pickFirstFreeTableNumber(counts)).toBe(3);
  });

  it('falls back to table 1 when every table has items', () => {
    const counts = new Map<number, number>(POS_FULL);
    expect(pickFirstFreeTableNumber(counts)).toBe(1);
  });
});

