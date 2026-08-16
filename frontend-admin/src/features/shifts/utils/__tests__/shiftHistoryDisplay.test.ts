import { describe, expect, it } from 'vitest';

import {
  cashierInitial,
  differenceTextColor,
  filterShiftHistory,
  groupShiftHistoryByRegister,
  shiftStatusTagColor,
  shortUserId,
  summarizeShiftHistory,
} from '../shiftHistoryDisplay';

describe('shiftHistoryDisplay', () => {
  it('shortens user ids', () => {
    expect(shortUserId('a1b2c3d4-e5f6-7890-abcd-ef1234567890')).toBe('a1b2c3d4');
    expect(shortUserId('short')).toBe('short');
    expect(shortUserId(null)).toBe('—');
  });

  it('uses first letter of cashier name', () => {
    expect(cashierInitial('Cash ler')).toBe('C');
    expect(cashierInitial('  anna')).toBe('A');
    expect(cashierInitial('')).toBe('?');
  });

  it('maps status and difference colors', () => {
    expect(shiftStatusTagColor('Completed')).toBe('success');
    expect(shiftStatusTagColor('Discrepancy')).toBe('warning');
    expect(shiftStatusTagColor('Active')).toBe('error');
    expect(differenceTextColor(1)).toBe('#389e0d');
    expect(differenceTextColor(-1)).toBe('#cf1322');
    expect(differenceTextColor(0)).toBe('#8c8c8c');
  });

  it('filters by cashier, status, and search', () => {
    const rows = [
      {
        cashierId: 'u1',
        cashierName: 'Alice',
        cashRegisterId: 'r1',
        registerNumber: 'KASSE-001',
        status: 'Completed',
      },
      {
        cashierId: 'u2',
        cashierName: 'Bob',
        cashRegisterId: 'r2',
        registerNumber: 'KASSE-002',
        status: 'Discrepancy',
      },
    ];
    expect(filterShiftHistory(rows, { cashierId: 'u1' })).toHaveLength(1);
    expect(filterShiftHistory(rows, { status: 'Discrepancy' })).toHaveLength(1);
    expect(filterShiftHistory(rows, { search: 'kasse-002' })).toHaveLength(1);
    expect(filterShiftHistory(rows, { search: 'ali' })).toHaveLength(1);
  });

  it('summarizes and groups history', () => {
    const rows = [
      {
        cashRegisterId: 'r2',
        registerNumber: 'KASSE-002',
        startedAt: '2026-08-11T10:00:00Z',
        totalSales: 10,
        totalCash: 4,
        totalCard: 6,
        difference: -1,
      },
      {
        cashRegisterId: 'r1',
        registerNumber: 'KASSE-001',
        startedAt: '2026-08-12T10:00:00Z',
        totalSales: 5,
        totalCash: 5,
        totalCard: 0,
        difference: 0,
      },
    ];
    expect(summarizeShiftHistory(rows)).toEqual({
      count: 2,
      totalSales: 15,
      totalCash: 9,
      totalCard: 6,
      totalDifference: -1,
    });
    const groups = groupShiftHistoryByRegister(rows);
    expect(groups.map((g) => g.registerLabel)).toEqual(['KASSE-001', 'KASSE-002']);
    expect(groups[0]?.shifts).toHaveLength(1);
  });
});
