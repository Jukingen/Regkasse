import { afterEach, describe, expect, it } from 'vitest';

import dayjs, {
  EMPTY_DATE_DISPLAY,
  formatDate,
  formatDateTime,
  formatDateTimeSeconds,
  formatExportFilenameDate,
  formatIsoDate,
  formatMonthYear,
  formatUtcDateTime,
  getDateLocale,
  getIsoWeekdayNames,
  getWeekdayNames,
  setDateLocale,
} from '@/lib/dateUtils';

describe('dateUtils locale', () => {
  afterEach(() => {
    setDateLocale('de');
  });

  it('defaults to German and formats month names in German', () => {
    setDateLocale('de');
    expect(getDateLocale()).toBe('de');
    expect(formatMonthYear('2026-07-15')).toMatch(/Juli/);
    expect(formatDate('2026-07-15')).toBe('15.07.2026');
  });

  it('switches to English and Turkish for named months', () => {
    setDateLocale('en');
    expect(formatMonthYear('2026-07-15')).toMatch(/July/);

    setDateLocale('tr');
    expect(formatMonthYear('2026-07-15')).toMatch(/Temmuz/);
  });

  it('falls back to German for unsupported locales', () => {
    expect(setDateLocale('fr')).toBe('de');
    expect(getDateLocale()).toBe('de');
  });

  it('returns localized weekday names for the active locale', () => {
    setDateLocale('de');
    const shortDe = getWeekdayNames(true);
    expect(shortDe).toHaveLength(7);
    expect(shortDe[0].toLowerCase()).toMatch(/^mo/);

    setDateLocale('en');
    const longEn = getWeekdayNames(false);
    expect(longEn).toHaveLength(7);
    expect(longEn.some((d) => /monday/i.test(d))).toBe(true);
  });

  it('returns ISO Monday-first weekday labels independent of locale week start', () => {
    const de = getIsoWeekdayNames('de', true);
    const en = getIsoWeekdayNames('en', true);
    expect(de).toHaveLength(7);
    expect(en).toHaveLength(7);
    expect(de[0].toLowerCase()).toMatch(/^mo/);
    expect(en[0].toLowerCase()).toMatch(/^mo/);
    expect(de[6].toLowerCase()).toMatch(/^so/);
    expect(en[6].toLowerCase()).toMatch(/^su/);
  });

  it('formats relative time using the active locale', () => {
    setDateLocale('de');
    const relative = dayjs().subtract(5, 'minute').fromNow();
    expect(relative.toLowerCase()).toMatch(/minute|minuten/);
  });

  it('returns empty placeholder for invalid dates', () => {
    expect(formatDate(null)).toBe(EMPTY_DATE_DISPLAY);
    expect(formatDateTime('')).toBe(EMPTY_DATE_DISPLAY);
    expect(formatDateTimeSeconds('nope')).toBe(EMPTY_DATE_DISPLAY);
  });

  it('formats datetime with seconds and ISO export stamps', () => {
    expect(formatDateTimeSeconds('2026-07-15T10:30:45')).toMatch(/15\.07\.2026/);
    expect(formatIsoDate('2026-07-15T10:30:45Z')).toBe('2026-07-15');
    expect(formatExportFilenameDate('2026-07-15')).toBe('2026-07-15');
    expect(formatUtcDateTime('2026-07-15T10:30:45Z')).toBe('15.07.2026 10:30:45');
  });
});
