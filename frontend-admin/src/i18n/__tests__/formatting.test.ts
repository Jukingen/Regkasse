import { describe, expect, it } from 'vitest';

import {
  FORMAT_EMPTY_DISPLAY,
  createIntlFormatters,
  formatBytes,
  formatCurrency,
  formatDate,
  formatDateTime,
  formatNumber,
  formatPercent,
} from '@/i18n/formatting';

describe('i18n formatting', () => {
  const locale = 'de-AT';

  it('formatCurrency formats EUR and custom currency; rejects non-finite', () => {
    expect(formatCurrency(19.9, locale)).toMatch(/19[,.]90/);
    expect(formatCurrency(10, locale, { currency: 'USD' })).toMatch(/10/);
    expect(formatCurrency(Number.NaN, locale)).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatCurrency(Infinity, locale)).toBe(FORMAT_EMPTY_DISPLAY);
  });

  it('formatDate returns em dash for empty/invalid and German day.month.year for valid', () => {
    expect(formatDate(null)).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatDate('')).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatDate('not-a-date')).toBe(FORMAT_EMPTY_DISPLAY);
    const local = new Date(2025, 11, 1);
    expect(formatDate(local, locale)).toBe('01.12.2025');
  });

  it('formatDate supports month-day-only Intl options', () => {
    const local = new Date(2025, 5, 15);
    expect(formatDate(local, locale, { month: '2-digit', day: '2-digit' })).toBe('15.06.');
  });

  it('formatDateTime handles empty, date-only, and time options', () => {
    expect(formatDateTime(undefined)).toBe(FORMAT_EMPTY_DISPLAY);
    const local = new Date(2025, 11, 1, 14, 30, 45);
    expect(formatDateTime(local, locale, { dateStyle: 'short' })).toBe('01.12.2025');
    expect(formatDateTime(local, locale, { hour: '2-digit', minute: '2-digit' })).toBe(
      '01.12.2025 14:30'
    );
    expect(
      formatDateTime(local, locale, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
    ).toBe('01.12.2025 14:30:45');
    expect(formatDateTime(local, locale)).toBe('01.12.2025 14:30');
  });

  it('formatNumber / formatPercent / formatBytes handle edge cases', () => {
    expect(formatNumber(Number.NaN, locale)).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatNumber(1234.5, locale)).toMatch(/1/);
    expect(formatPercent(Number.NaN, locale)).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatPercent(0.25, locale)).toMatch(/25/);
    expect(formatBytes(-1, locale)).toBe(FORMAT_EMPTY_DISPLAY);
    expect(formatBytes(0, locale)).toBe('0 B');
    expect(formatBytes(1024, locale)).toMatch(/KB/);
  });

  it('createIntlFormatters binds locale helpers', () => {
    const fmt = createIntlFormatters(locale);
    expect(fmt.formatLocale).toBe(locale);
    expect(fmt.formatCurrency(1)).toMatch(/1[,.]00/);
    expect(fmt.formatDate(new Date(2025, 0, 2))).toBe('02.01.2025');
    expect(fmt.formatBytes(0)).toBe('0 B');
  });
});
