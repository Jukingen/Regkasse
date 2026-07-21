import { describe, expect, it } from 'vitest';

import {
  aggregateMissingMonatsbelegeForCompliance,
  collectPastMissingMonatsbelege,
  computeDaysLateFromDeadline,
  countPastMissingMonatsbelege,
  filterPastMissingMonths,
  formatMonatsbelegMonthNameDe,
  isCurrentViennaMonth,
  isPastViennaMonth,
} from '@/features/rksv/utils/monatsbelegMissingMonths';

describe('monatsbelegMissingMonths', () => {
  const now = new Date('2026-06-15T12:00:00.000Z');

  it('formats German month names', () => {
    expect(formatMonatsbelegMonthNameDe(1).toLowerCase()).toContain('januar');
  });

  it('computes days late from deadline', () => {
    expect(computeDaysLateFromDeadline(null, now)).toBe(0);
    expect(computeDaysLateFromDeadline('2026-06-20', now)).toBe(0);
    expect(computeDaysLateFromDeadline('2026-06-01', now)).toBeGreaterThan(0);
  });

  it('detects current vs past Vienna months', () => {
    const { year, month } = { year: 2026, month: 6 };
    // June 15 UTC is June in Vienna for this fixture
    expect(isCurrentViennaMonth(year, month, now) || isPastViennaMonth(year, month, now)).toBe(
      true,
    );
    expect(isPastViennaMonth(2026, 1, now)).toBe(true);
    expect(filterPastMissingMonths([{ year: 2026, month: 1, isOverdue: true }], now)).toHaveLength(
      1,
    );
  });

  it('aggregates and collects missing months across registers', () => {
    const overview = [
      {
        cashRegisterId: 'reg-a',
        status: {
          missingMonths: [
            { year: 2026, month: 1, isOverdue: true, deadline: '2026-02-07' },
            { year: 2026, month: 2, isOverdue: false, deadline: '2026-03-07' },
          ],
        },
      },
      {
        cashRegisterId: 'reg-b',
        status: {
          missingMonths: [{ year: 2026, month: 1, isOverdue: true, deadline: '2026-02-01' }],
        },
      },
    ];

    const aggregated = aggregateMissingMonatsbelegeForCompliance(overview, now);
    expect(aggregated.length).toBeGreaterThanOrEqual(1);
    expect(aggregated[0]?.month).toBe(1);

    const past = collectPastMissingMonatsbelege(overview, now);
    expect(past.some((e) => e.cashRegisterId === 'reg-a')).toBe(true);
    expect(countPastMissingMonatsbelege(overview, now)).toBe(past.length);
    expect(aggregateMissingMonatsbelegeForCompliance(undefined, now)).toEqual([]);
  });
});
