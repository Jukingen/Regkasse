import { describe, expect, it } from 'vitest';

import {
  formatCountryCurrency,
  formatCountryDate,
  formatCountryNumber,
  getCountryFormatProfile,
} from '@/lib/countryFormatProfiles';

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
});
