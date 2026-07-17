import { describe, expect, it } from '@jest/globals';

import {
  DEFAULT_TEXT_LOCALE,
  getFormattingLocaleForTextLocale,
  normalizeFormatLocale,
  normalizeTextLocale,
  SUPPORTED_TEXT_LOCALES,
  toUserSettingsLanguage,
} from '../i18n/localeUtils';

describe('localeUtils', () => {
  it('supports de, en, and tr text locales', () => {
    expect([...SUPPORTED_TEXT_LOCALES]).toEqual(['de', 'en', 'tr']);
    expect(DEFAULT_TEXT_LOCALE).toBe('de');
  });

  it('normalizes API and BCP-47 tags to POS text locales', () => {
    expect(normalizeTextLocale('de')).toBe('de');
    expect(normalizeTextLocale('de-DE')).toBe('de');
    expect(normalizeTextLocale('en')).toBe('en');
    expect(normalizeTextLocale('en-US')).toBe('en');
    expect(normalizeTextLocale('tr')).toBe('tr');
    expect(normalizeTextLocale('tr-TR')).toBe('tr');
    expect(normalizeTextLocale(null)).toBe('de');
    expect(normalizeTextLocale('fr-FR')).toBe('de');
  });

  it('maps text locales to formatting locales', () => {
    expect(normalizeFormatLocale('de-AT')).toBe('de-AT');
    expect(normalizeFormatLocale('en')).toBe('en-US');
    expect(normalizeFormatLocale('tr-TR')).toBe('tr-TR');
    expect(getFormattingLocaleForTextLocale('en-GB')).toBe('en-US');
  });

  it('maps POS locales to UserSettings API language codes', () => {
    expect(toUserSettingsLanguage('de')).toBe('de-DE');
    expect(toUserSettingsLanguage('en')).toBe('en');
    expect(toUserSettingsLanguage('tr')).toBe('tr');
  });
});
