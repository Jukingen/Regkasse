import { describe, expect, it } from 'vitest';

import {
  formatCountryCurrency,
  formatCountryDate,
  formatCountryNumber,
  getCountryFormatProfile,
  resolveCountryFormatProfile,
} from '@/lib/countryFormatProfiles';

/** Stand-in for `GET /api/admin/countries`. */
const COUNTRIES_API = [
  { code: 'AT', currency: 'EUR', defaultLocale: 'de-DE' },
  { code: 'DE', currency: 'EUR', defaultLocale: 'de-DE' },
  { code: 'CH', currency: 'CHF', defaultLocale: 'de-CH' },
];

describe('countryFormatProfiles', () => {
  it('formats AT calendar dates as DD.MM.YYYY', () => {
    const at = getCountryFormatProfile('AT');
    expect(formatCountryDate('2026-01-15', at)).toBe('15.01.2026');
  });

  it('formats DE calendar dates as DD.MM.YYYY', () => {
    const de = getCountryFormatProfile('DE');
    expect(formatCountryDate('2026-01-15', de)).toBe('15.01.2026');
  });

  it("formats CH numbers with apostrophe thousands and dot decimal", () => {
    const ch = getCountryFormatProfile('CH');
    expect(ch.thousandsSeparator).toBe("'");
    expect(ch.decimalSeparator).toBe('.');
    expect(formatCountryNumber(1234.56, ch)).toBe("1'234.56");
  });

  it('formats EU_DEFAULT calendar dates as YYYY-MM-DD', () => {
    const eu = getCountryFormatProfile('EU_DEFAULT');
    expect(formatCountryDate('2026-01-15', eu)).toBe('2026-01-15');
  });

  it('falls back to AT for unknown or blank country codes', () => {
    expect(getCountryFormatProfile('XX').code).toBe('AT');
    expect(getCountryFormatProfile(null).code).toBe('AT');
    expect(getCountryFormatProfile('').code).toBe('AT');
    expect(getCountryFormatProfile('ch').code).toBe('CH');
    expect(formatCountryDate('2026-01-15', getCountryFormatProfile('ZZ'))).toBe('15.01.2026');
  });

  it('formats AT currency with euro suffix', () => {
    expect(formatCountryCurrency(12.5, getCountryFormatProfile('AT'))).toBe('12,50 €');
  });

  it('formats EUR without branching on AT or DE country codes', () => {
    expect(
      formatCountryCurrency(12.5, {
        code: 'XX',
        locale: 'en',
        currency: 'EUR',
        timeZone: 'UTC',
        dateFormat: 'YYYY-MM-DD',
        decimalSeparator: '.',
        thousandsSeparator: ',',
      })
    ).toBe('12.50 €');
  });

  it('uses the countries API currency and locale when the catalog is loaded', () => {
    const de = resolveCountryFormatProfile('DE', [
      ...COUNTRIES_API.slice(0, 1),
      { code: 'DE', currency: 'EUR', defaultLocale: 'de-DE' },
      COUNTRIES_API[2],
    ]);
    expect(de.currency).toBe('EUR');
    expect(de.locale).toBe('de-DE');
    expect(de.timeZone).toBe('Europe/Berlin');
    const overridden = resolveCountryFormatProfile('DE', [
      { code: 'DE', currency: 'EUR', defaultLocale: 'fr-DE' },
    ]);
    expect(overridden.locale).toBe('fr-DE');
    expect(overridden.timeZone).toBe('Europe/Berlin');
  });

  it('keeps the local map only when the countries API payload is unavailable', () => {
    expect(resolveCountryFormatProfile('CH', null).currency).toBe('CHF');
    expect(resolveCountryFormatProfile('CH', null).locale).toBe('de-CH');
    expect(resolveCountryFormatProfile('DE', undefined).timeZone).toBe('Europe/Berlin');
  });
});
