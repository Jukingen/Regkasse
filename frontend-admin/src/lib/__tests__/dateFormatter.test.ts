import { describe, expect, it } from 'vitest';
import {
  formatGermanDate,
  formatGermanDateTime,
  formatGermanTime,
  formatUserDate,
  formatUserDateTime,
  formatUserMonthDay,
  formatUserMonthYear,
} from '../dateFormatter';

describe('dateFormatter', () => {
  it('formats ISO UTC to DD.MM.YYYY in local timezone', () => {
    const iso = '2025-12-01T10:30:00Z';
    const expected = formatUserDate(new Date(iso));
    expect(expected).toMatch(/^\d{2}\.\d{2}\.2025$/);
    expect(expected.split('.').length).toBe(3);
  });

  it('formats datetime without seconds by default', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatUserDateTime(local)).toBe('01.12.2025 14:30');
  });

  it('formats datetime with seconds when requested', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatUserDateTime(local, { includeSeconds: true })).toBe('01.12.2025 14:30:45');
  });

  it('returns empty string for invalid input', () => {
    expect(formatUserDate(null)).toBe('');
    expect(formatUserDate('')).toBe('');
    expect(formatUserDate('not-a-date')).toBe('');
  });

  it('formats month-day without year', () => {
    const local = new Date(2025, 11, 1);
    expect(formatUserMonthDay(local)).toBe('01.12.');
  });

  it('formats month-year from date or YYYY-MM string', () => {
    expect(formatUserMonthYear(new Date(2025, 5, 1))).toBe('06.2025');
    expect(formatUserMonthYear('2025-06-01')).toBe('06.2025');
  });

  it('formatGerman* returns em dash for missing or invalid input', () => {
    expect(formatGermanDate(null)).toBe('—');
    expect(formatGermanDateTime(undefined)).toBe('—');
    expect(formatGermanTime('')).toBe('—');
    expect(formatGermanDateTime('not-a-date')).toBe('—');
  });

  it('formatGerman* uses fixed DD.MM.YYYY / HH:mm layout', () => {
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatGermanDate(local)).toBe('01.12.2025');
    expect(formatGermanDateTime(local)).toBe('01.12.2025 14:30');
    expect(formatGermanTime(local)).toBe('14:30');
  });
});
