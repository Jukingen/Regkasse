import { describe, expect, it } from 'vitest';

import { getCatalog } from '@/i18n/config';
import {
  DATE_FORMAT_FALLBACKS,
  formatLocalizedDate,
  resolveDateFormatString,
} from '@/lib/formattedDate';
import { EMPTY_DATE_DISPLAY } from '@/lib/dateUtils';

function catalogTranslate(locale: 'de' | 'en' | 'tr') {
  const catalog = getCatalog(locale).common as Record<string, unknown>;
  return (key: string): string => {
    const path = key.replace(/^common\./, '').split('.');
    let cur: unknown = catalog;
    for (const segment of path) {
      if (!cur || typeof cur !== 'object') return '';
      cur = (cur as Record<string, unknown>)[segment];
    }
    return typeof cur === 'string' ? cur : '';
  };
}

describe('formattedDate (i18n patterns)', () => {
  it('resolves short/medium/long patterns from de catalog', () => {
    const t = catalogTranslate('de');
    expect(resolveDateFormatString(t, 'short')).toBe('DD.MM.YYYY');
    expect(resolveDateFormatString(t, 'medium')).toBe('DD. MMMM YYYY');
    expect(resolveDateFormatString(t, 'long')).toBe('DD. MMMM YYYY HH:mm');
    expect(resolveDateFormatString(t, 'datetime')).toBe('DD.MM.YYYY HH:mm');
    expect(resolveDateFormatString(t, 'datetimeSeconds')).toBe('DD.MM.YYYY HH:mm:ss');
  });

  it('falls back when translate returns empty', () => {
    expect(resolveDateFormatString(() => '', 'short')).toBe(DATE_FORMAT_FALLBACKS.short);
  });

  it('formats short dates with Austrian DD.MM.YYYY', () => {
    const t = catalogTranslate('de');
    expect(formatLocalizedDate('2026-07-15', 'short', 'de', t)).toBe('15.07.2026');
  });

  it('formats medium dates with locale-aware month names', () => {
    expect(formatLocalizedDate('2026-07-15', 'medium', 'de', catalogTranslate('de'))).toMatch(
      /Juli/
    );
    expect(formatLocalizedDate('2026-07-15', 'medium', 'en', catalogTranslate('en'))).toMatch(
      /July/
    );
    expect(formatLocalizedDate('2026-07-15', 'medium', 'tr', catalogTranslate('tr'))).toMatch(
      /Temmuz/
    );
  });

  it('returns empty placeholder for invalid input', () => {
    const t = catalogTranslate('de');
    expect(formatLocalizedDate(null, 'short', 'de', t)).toBe(EMPTY_DATE_DISPLAY);
    expect(formatLocalizedDate('not-a-date', 'short', 'de', t)).toBe(EMPTY_DATE_DISPLAY);
  });
});
