import { describe, expect, it } from '@jest/globals';

import {
  DEFAULT_TEXT_LOCALE,
  getFormattingLocaleForTextLocale,
  matchSupportedTextLocale,
  normalizeFormatLocale,
  normalizeTextLocale,
  resolveInitialTextLocale,
  resolveTextLocaleFromDeviceLocales,
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
    expect(normalizeTextLocale('de_AT')).toBe('de');
    expect(normalizeTextLocale('en')).toBe('en');
    expect(normalizeTextLocale('en-US')).toBe('en');
    expect(normalizeTextLocale('tr')).toBe('tr');
    expect(normalizeTextLocale('tr-TR')).toBe('tr');
    expect(normalizeTextLocale(null)).toBe('de');
    expect(normalizeTextLocale('fr-FR')).toBe('de');
  });

  it('matchSupportedTextLocale returns null for unsupported languages', () => {
    expect(matchSupportedTextLocale('fr-FR')).toBeNull();
    expect(matchSupportedTextLocale('pl')).toBeNull();
    expect(matchSupportedTextLocale(null)).toBeNull();
    expect(matchSupportedTextLocale('en-GB')).toBe('en');
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

  describe('device language detection (expo-localization getLocales)', () => {
    it('uses German when device primary language is German', () => {
      expect(
        resolveTextLocaleFromDeviceLocales([{ languageCode: 'de', languageTag: 'de-AT' }])
      ).toBe('de');
    });

    it('uses English when device primary language is English', () => {
      expect(
        resolveTextLocaleFromDeviceLocales([{ languageCode: 'en', languageTag: 'en-US' }])
      ).toBe('en');
    });

    it('uses Turkish when device primary language is Turkish', () => {
      expect(
        resolveTextLocaleFromDeviceLocales([{ languageCode: 'tr', languageTag: 'tr-TR' }])
      ).toBe('tr');
    });

    it('falls back to German when primary language is unsupported', () => {
      expect(
        resolveTextLocaleFromDeviceLocales([{ languageCode: 'fr', languageTag: 'fr-FR' }])
      ).toBe('de');
      expect(
        resolveTextLocaleFromDeviceLocales([
          { languageCode: 'pl', languageTag: 'pl-PL' },
          { languageCode: 'it', languageTag: 'it-IT' },
        ])
      ).toBe('de');
    });

    it('picks the first supported language from the preferred-locale list', () => {
      // Device: French first, English second → POS should use English, not German.
      expect(
        resolveTextLocaleFromDeviceLocales([
          { languageCode: 'fr', languageTag: 'fr-FR' },
          { languageCode: 'en', languageTag: 'en-GB' },
          { languageCode: 'de', languageTag: 'de-DE' },
        ])
      ).toBe('en');
    });

    it('falls back to German when locales are empty or missing', () => {
      expect(resolveTextLocaleFromDeviceLocales([])).toBe('de');
      expect(resolveTextLocaleFromDeviceLocales(null)).toBe('de');
      expect(resolveTextLocaleFromDeviceLocales(undefined)).toBe('de');
      expect(resolveTextLocaleFromDeviceLocales([{ languageCode: null, languageTag: null }])).toBe(
        'de'
      );
    });

    it('prefers languageCode over languageTag when both are present', () => {
      expect(
        resolveTextLocaleFromDeviceLocales([{ languageCode: 'tr', languageTag: 'en-US' }])
      ).toBe('tr');
    });

    it('uses languageTag when languageCode is missing', () => {
      expect(resolveTextLocaleFromDeviceLocales([{ languageTag: 'en-US' }])).toBe('en');
    });
  });

  describe('resolveInitialTextLocale', () => {
    it('prefers saved language over device locales', () => {
      expect(
        resolveInitialTextLocale({
          savedLanguage: 'tr',
          deviceLocales: [{ languageCode: 'en', languageTag: 'en-US' }],
        })
      ).toBe('tr');
    });

    it('uses device locales when no saved language', () => {
      expect(
        resolveInitialTextLocale({
          savedLanguage: null,
          deviceLocales: [{ languageCode: 'en', languageTag: 'en-US' }],
        })
      ).toBe('en');
    });

    it('falls back to German when saved is invalid and device is unsupported', () => {
      expect(
        resolveInitialTextLocale({
          savedLanguage: 'xx-YY',
          deviceLocales: [{ languageCode: 'ja', languageTag: 'ja-JP' }],
        })
      ).toBe('de');
    });

    it('falls back to German when detection inputs are empty', () => {
      expect(resolveInitialTextLocale({})).toBe('de');
      expect(resolveInitialTextLocale({ savedLanguage: null, deviceLocales: [] })).toBe('de');
    });
  });
});
