import { describe, expect, it } from 'vitest';

import {
  formatViennaCalendarDate,
  formatViennaYearMonth,
  getMonthDifference,
  getViennaCalendarDate,
  getViennaCalendarYear,
  getViennaCalendarYearMonth,
  isSameViennaCalendarDay,
} from '@/shared/utils/viennaCalendar';

describe('viennaCalendar', () => {
  const fixed = new Date('2026-03-15T12:00:00.000Z');

  it('reads Vienna calendar year/month/day', () => {
    expect(getViennaCalendarYear(fixed)).toBe(2026);
    expect(getViennaCalendarYearMonth(fixed)).toEqual({ year: 2026, month: 3 });
    expect(getViennaCalendarDate(fixed).year).toBe(2026);
    expect(getViennaCalendarDate(fixed).month).toBe(3);
  });

  it('formats year-month and calendar date', () => {
    expect(formatViennaYearMonth(2026, 3)).toBe('2026-03');
    expect(formatViennaYearMonth(2026, 9)).toBe('2026-09');
    expect(formatViennaCalendarDate(fixed)).toMatch(/^2026-03-\d{2}$/);
  });

  it('computes month difference vs now', () => {
    expect(getMonthDifference(2026, 3, fixed)).toBe(0);
    expect(getMonthDifference(2026, 2, fixed)).toBe(1);
    expect(getMonthDifference(2026, 4, fixed)).toBe(-1);
  });

  it('compares Vienna calendar days with edge cases', () => {
    expect(isSameViennaCalendarDay(null, fixed)).toBe(false);
    expect(isSameViennaCalendarDay('  ', fixed)).toBe(false);
    expect(isSameViennaCalendarDay('not-a-date', fixed)).toBe(false);
    expect(isSameViennaCalendarDay(fixed.toISOString(), fixed)).toBe(true);
  });
});
