import { describe, expect, it } from 'vitest';

import { filterHistoryByDayKind, isEmptyDailyClosing } from '../dayKind';

describe('tagesabschluss dayKind', () => {
  it('treats isEmpty and dayKind=empty as empty closings', () => {
    expect(isEmptyDailyClosing({ isEmpty: true })).toBe(true);
    expect(isEmptyDailyClosing({ dayKind: 'empty' })).toBe(true);
    expect(isEmptyDailyClosing({ closingType: 'Daily', transactionCount: 0 })).toBe(true);
    expect(isEmptyDailyClosing({ closingType: 'Daily', transactionCount: 2, isEmpty: false })).toBe(
      false
    );
    expect(isEmptyDailyClosing({ closingType: 'Monthly', transactionCount: 0 })).toBe(false);
  });

  it('filters history by normal vs empty', () => {
    const rows = [
      { closingId: 'n', transactionCount: 3, isEmpty: false, dayKind: 'normal' as const },
      { closingId: 'e', transactionCount: 0, isEmpty: true, dayKind: 'empty' as const },
    ];
    expect(filterHistoryByDayKind(rows, 'all')).toHaveLength(2);
    expect(filterHistoryByDayKind(rows, 'empty').map((r) => r.closingId)).toEqual(['e']);
    expect(filterHistoryByDayKind(rows, 'normal').map((r) => r.closingId)).toEqual(['n']);
  });
});
