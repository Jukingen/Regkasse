import { describe, expect, it } from 'vitest';

import { deDE, enUS, getAntdLocale, trTR } from '@/lib/antdLocale';

describe('getAntdLocale', () => {
  it('maps Admin text locales to Ant Design packages', () => {
    expect(getAntdLocale('de')).toBe(deDE);
    expect(getAntdLocale('en')).toBe(enUS);
    expect(getAntdLocale('tr')).toBe(trTR);
  });

  it('accepts BCP-47 format locales used by personalization', () => {
    expect(getAntdLocale('de-AT')).toBe(deDE);
    expect(getAntdLocale('de-DE')).toBe(deDE);
    expect(getAntdLocale('en-US')).toBe(enUS);
    expect(getAntdLocale('tr-TR')).toBe(trTR);
  });

  it('defaults to German for missing or unsupported locales', () => {
    expect(getAntdLocale(undefined)).toBe(deDE);
    expect(getAntdLocale(null)).toBe(deDE);
    expect(getAntdLocale('fr')).toBe(deDE);
  });
});
