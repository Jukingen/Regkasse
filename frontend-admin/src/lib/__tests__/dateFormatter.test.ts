import { describe, expect, it } from 'vitest';

import {
  GERMAN_DATE_EMPTY,
  formatDate,
  formatDateTime,
  formatGermanDate,
  formatGermanDateTime,
  formatGermanTime,
  formatUserDate,
  formatUserDateTime,
  formatUserMonthDay,
  formatUserMonthYear,
  formatUserTime,
  toDayjsDateFormat,
} from '@/lib/dateFormatter';

describe('dateFormatter', () => {
  const d = new Date(2025, 11, 1, 10, 30, 45); // local Dec 1 2025

  it('returns empty for null/invalid', () => {
    expect(formatUserDate(null)).toBe('');
    expect(formatUserDate('')).toBe('');
    expect(formatUserDate('not-a-date')).toBe('');
  });

  it('formats date and aliases', () => {
    expect(formatUserDate(d)).toBe('01.12.2025');
    expect(formatDate(d)).toBe('01.12.2025');
    expect(formatUserMonthDay(d)).toBe('01.12.');
    expect(formatUserMonthYear(d)).toBe('12.2025');
    expect(formatUserMonthYear('2025-06')).toBe('06.2025');
  });

  it('formats date-time and time with optional seconds', () => {
    expect(formatUserDateTime(d)).toMatch(/^01\.12\.2025 10:30$/);
    expect(formatUserDateTime(d, { includeSeconds: true })).toMatch(/^01\.12\.2025 10:30:45$/);
    expect(formatDateTime(d)).toMatch(/01\.12\.2025/);
    expect(formatUserTime(d)).toMatch(/^10:30$/);
    expect(formatUserTime(d, { includeSeconds: true })).toMatch(/^10:30:45$/);
  });

  it('exposes empty placeholder constant', () => {
    expect(GERMAN_DATE_EMPTY).toBe('—');
  });

  it('uses German placeholders for missing values', () => {
    expect(formatGermanDate(null)).toBe('—');
    expect(formatGermanDateTime(undefined)).toBe('—');
    expect(formatGermanTime('')).toBe('—');
    expect(formatGermanDate(d)).toBe('01.12.2025');
    expect(toDayjsDateFormat()).toBe('DD.MM.YYYY');
  });
});
